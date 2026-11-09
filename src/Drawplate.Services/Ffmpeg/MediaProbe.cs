using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Ffmpeg;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

/// <summary>Reads duration and whether a video stream exists. ffprobe is optional: without it (or when it fails) the FFmpeg banner is read.</summary>
public sealed class MediaProbe
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessLauncher _launcher;
    private readonly string _ffmpegPath;
    private readonly string? _ffprobePath;

    public MediaProbe(IProcessLauncher launcher, string ffmpegPath, string? ffprobePath)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(ffmpegPath);
        _launcher = launcher;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    public async Task<MediaInfo> InspectAsync(string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!string.IsNullOrEmpty(_ffprobePath) && await TryFfprobeAsync(_ffprobePath, path, ct).ConfigureAwait(false) is { } info)
        {
            return info;
        }

        return await ReadBannerAsync(path, ct).ConfigureAwait(false);
    }

    private static TimeSpan? ParseSeconds(string output) =>
        double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    /// <summary>Null when ffprobe cannot be started, times out or fails, so the caller falls back to the banner.</summary>
    private async Task<MediaInfo?> TryFfprobeAsync(string ffprobePath, string path, CancellationToken ct)
    {
        var durationOutput = await RunAsync(
            ffprobePath,
            ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
            requireSuccess: true,
            ct).ConfigureAwait(false);
        if (durationOutput is null)
        {
            return null;
        }

        var streamOutput = await RunAsync(
            ffprobePath,
            ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_type", "-of", "csv=p=0", path],
            requireSuccess: true,
            ct).ConfigureAwait(false);
        return streamOutput is null
            ? null
            : new MediaInfo(ParseSeconds(durationOutput.Stdout), streamOutput.Stdout.Trim().Length > 0);
    }

    /// <summary>The banner (<c>ffmpeg -i file</c>) goes to stderr and exits 1 by design, so the exit code is ignored.</summary>
    private async Task<MediaInfo> ReadBannerAsync(string path, CancellationToken ct)
    {
        var output = await RunAsync(_ffmpegPath, FfmpegArgv.Banner(path), requireSuccess: false, ct).ConfigureAwait(false);
        return output is null
            ? new MediaInfo(null, false)
            : new MediaInfo(FfmpegBannerParser.ParseDuration(output.Stderr), FfmpegBannerParser.HasVideoStream(output.Stderr));
    }

    /// <summary>Runs one command. Null on start failure, timeout, or (when required) a non-zero exit; cancellation by the caller propagates.</summary>
    private async Task<CommandOutput?> RunAsync(string executable, IReadOnlyList<string> arguments, bool requireSuccess, CancellationToken ct)
    {
        IRunningProcess process;
        try
        {
            process = _launcher.Start(executable, arguments, new ProcessOptions(CommandTimeout, Cancellation: ct));
        }
        catch (ProcessStartFailedException)
        {
            return null;
        }

        try
        {
            var stdout = new List<string>();
            await foreach (var line in process.StdoutLines.ConfigureAwait(false))
            {
                stdout.Add(line);
            }

            var exitCode = await process.WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            return process.TimedOut || (requireSuccess && exitCode != 0)
                ? null
                : new CommandOutput(string.Join('\n', stdout), process.StderrTail);
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record CommandOutput(string Stdout, string Stderr);
}
