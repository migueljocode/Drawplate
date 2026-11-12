using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Ffmpeg;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

public sealed class FfmpegInfoService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly Version MinimumVersion = new(6, 0);

    private readonly IProcessLauncher _launcher;
    private readonly Func<string, bool> _fileExists;

    public FfmpegInfoService(IProcessLauncher launcher, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>Runs <c>-version</c> and <c>-encoders</c>. Cancellation by the caller propagates; every other failure becomes a reason.</summary>
    public async Task<FfmpegInfoResult> GetInfoAsync(string ffmpegPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);

        if (!_fileExists(ffmpegPath))
        {
            return FfmpegInfoResult.Broken("file not found");
        }

        // Probe -version and -encoders concurrently so cold FFmpeg discovery pays one startup round-trip instead of two.
        var versionTask = RunAsync(ffmpegPath, "-version", ct);
        var encodersTask = RunAsync(ffmpegPath, "-encoders", ct);
        await Task.WhenAll(versionTask, encodersTask).ConfigureAwait(false);

        var (versionOutput, versionFailure) = await versionTask.ConfigureAwait(false);
        if (versionFailure is not null)
        {
            return FfmpegInfoResult.Broken(versionFailure);
        }

        var version = ParseVersion(versionOutput);
        if (version is null)
        {
            return FfmpegInfoResult.Broken("unreadable version output");
        }

        if (version.Version < MinimumVersion && !version.IsMaster)
        {
            return FfmpegInfoResult.Old(version.Version);
        }

        var (encodersOutput, encodersFailure) = await encodersTask.ConfigureAwait(false);
        if (encodersFailure is not null)
        {
            return FfmpegInfoResult.Broken(encodersFailure);
        }

        var encoders = FfmpegBannerParser.ParseEncoders(encodersOutput);
        return FfmpegInfoResult.Ok(new FfmpegInfo(ffmpegPath, version, encoders, encoders.Contains("libx264")));
    }

    /// <summary>The banner parser returns version 0.0 when it finds no version, and its Version constructor can throw on odd text.</summary>
    private static FfmpegVersion? ParseVersion(string output)
    {
        try
        {
            var version = FfmpegBannerParser.ParseVersion(output);
            return version.Version == new Version(0, 0) && !version.IsMaster ? null : version;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private async Task<(string Output, string? Failure)> RunAsync(string ffmpegPath, string argument, CancellationToken ct)
    {
        IRunningProcess process;
        try
        {
            process = _launcher.Start(ffmpegPath, [argument], new ProcessOptions(CommandTimeout, Cancellation: ct));
        }
        catch (ProcessStartFailedException)
        {
            return (string.Empty, "cannot be started");
        }

        try
        {
            var lines = new List<string>();
            await foreach (var line in process.StdoutLines.ConfigureAwait(false))
            {
                lines.Add(line);
            }

            var exitCode = await process.WaitAsync(ct).ConfigureAwait(false);

            // A cancelled run kills FFmpeg, so WaitAsync can return normally with the kill's exit code.
            ct.ThrowIfCancellationRequested();

            if (process.TimedOut)
            {
                return (string.Empty, "timed out");
            }

            return exitCode == 0
                ? (string.Join('\n', lines), null)
                : (string.Empty, $"exit code {exitCode}");
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }
}
