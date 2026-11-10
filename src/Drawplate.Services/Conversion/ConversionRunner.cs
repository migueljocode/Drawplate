using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
using Drawplate.Models.Planning;
using Drawplate.Models.Progress;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Conversion;

public sealed class ConversionRunner
{
    private readonly string _ffmpegPath;
    private readonly IProcessLauncher _launcher;
    private readonly MediaProbe _mediaProbe;
    private readonly ProgressParser _progressParser = new();

    public ConversionRunner(string ffmpegPath, IProcessLauncher launcher, MediaProbe mediaProbe)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(mediaProbe);
        _ffmpegPath = ffmpegPath;
        _launcher = launcher;
        _mediaProbe = mediaProbe;
    }

    public async Task<FileResult> RunAsync(
        string source,
        string outputFolder,
        ImmutableArray<EncoderKind> chain,
        int quality,
        SizeCap cap,
        bool noTimestamps,
        VaapiTarget? vaapi,
        Action<string>? log,
        FileProgressHandler? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(outputFolder);

        var targetName = OutputNaming.TargetName(Path.GetFileName(source));
        var target = Path.Combine(outputFolder, targetName);
        var partial = Path.Combine(outputFolder, OutputNaming.PartialName(Path.GetFileName(source), Environment.ProcessId));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (File.Exists(target))
            {
                return Skipped(source, target, "output already exists", sw.Elapsed);
            }

            var mediaInfo = await _mediaProbe.InspectAsync(source, ct).ConfigureAwait(false);
            if (!mediaInfo.HasVideo)
            {
                return Failed(source, target, "no video stream", sw.Elapsed);
            }

            if (File.Exists(partial))
            {
                File.Delete(partial);
            }

            string? lastStderr = null;
            FailureKind? lastFailureKind = null;

            foreach (var encoder in chain)
            {
                ct.ThrowIfCancellationRequested();

                if (encoder == EncoderKind.Vaapi && vaapi is null)
                {
                    // No render node was found by the probe, so this encoder cannot be used for this file.
                    lastStderr = "no VAAPI device";
                    lastFailureKind = FailureKind.DeviceAccess;
                    continue;
                }

                var args = FfmpegArgv.Convert(source, partial, encoder, quality, cap, vaapi);
                log?.Invoke("ffmpeg " + string.Join(' ', args));

                // No timeout: a long video may legitimately take hours. Cancellation kills the process tree.
                IRunningProcess process;
                try
                {
                    process = _launcher.Start(_ffmpegPath, args, new ProcessOptions(Cancellation: ct));
                }
                catch (ProcessStartFailedException)
                {
                    return Failed(source, target, "cannot start ffmpeg", sw.Elapsed);
                }

                var duration = mediaInfo.Duration;

                await foreach (var line in process.StdoutLines.ConfigureAwait(false))
                {
                    var sample = _progressParser.Feed(line);
                    if (sample is { } s)
                    {
                        var fraction = ProgressParser.Fraction(s, duration);
                        progress?.Invoke(source, fraction, s.Speed, s.Elapsed);
                    }
                }

                var exitCode = await process.WaitAsync(ct).ConfigureAwait(true);
                var stderrTail = process.StderrTail;

                try
                {
                    await process.DisposeAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                if (exitCode == 0 && File.Exists(partial) && new FileInfo(partial).Length > 0)
                {
                    try
                    {
                        File.Move(partial, target, overwrite: false);
                    }
                    catch (IOException)
                    {
                        if (File.Exists(partial))
                        {
                            File.Delete(partial);
                        }
                        return Skipped(source, target, "output already exists", sw.Elapsed);
                    }

                    if (!noTimestamps)
                    {
                        TimestampCopier.Copy(source, target, copyCreationTime: OperatingSystem.IsWindows());
                    }

                    return new FileResult
                    {
                        Status = FileResultStatus.Converted,
                        Source = source,
                        Target = target,
                        Encoder = EncoderSets.Id(encoder)!,
                        Reason = null,
                        InputSize = GetFileSize(source),
                        OutputSize = new FileInfo(target).Length,
                        Elapsed = sw.Elapsed,
                    };
                }

                lastStderr = stderrTail;
                lastFailureKind = FailureClassifier.Classify(stderrTail);
                if (!string.IsNullOrWhiteSpace(stderrTail))
                {
                    log?.Invoke(stderrTail);
                }

                if (File.Exists(partial))
                {
                    File.Delete(partial);
                }
            }

            return Failed(
                source,
                target,
                FailureClassifier.ShortReason(lastFailureKind ?? FailureKind.Unknown, lastStderr ?? ""),
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(partial))
            {
                try { File.Delete(partial); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            throw;
        }
        finally
        {
            sw.Stop();
            if (File.Exists(partial))
            {
                try { File.Delete(partial); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static FileResult Converted(string source, string target, string encoder, long inputSize, long outputSize, TimeSpan elapsed) =>
        new()
        {
            Status = FileResultStatus.Converted,
            Source = source,
            Target = target,
            Encoder = encoder,
            Reason = null,
            InputSize = inputSize,
            OutputSize = outputSize,
            Elapsed = elapsed,
        };

    private static FileResult Failed(string source, string target, string reason, TimeSpan elapsed) =>
        new()
        {
            Status = FileResultStatus.Failed,
            Source = source,
            Target = target,
            Encoder = null,
            Reason = reason,
            InputSize = GetFileSize(source),
            OutputSize = 0,
            Elapsed = elapsed,
        };

    private static FileResult Skipped(string source, string target, string reason, TimeSpan elapsed) =>
        new()
        {
            Status = FileResultStatus.Skipped,
            Source = source,
            Target = target,
            Encoder = null,
            Reason = reason,
            InputSize = GetFileSize(source),
            OutputSize = GetFileSize(target),
            Elapsed = elapsed,
        };

    private static long GetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return 0;
    }
}
