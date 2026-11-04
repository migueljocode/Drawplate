using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

public sealed class MediaProbe
{
    private readonly IProcessLauncher _launcher;
    private readonly string? _ffprobePath;

    public MediaProbe(IProcessLauncher launcher, string? ffprobePath = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
        _ffprobePath = ffprobePath;
    }

    public async Task<MediaInfo> InspectAsync(string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!string.IsNullOrEmpty(_ffprobePath))
        {
            var info = await TryFfprobeAsync(path, ct).ConfigureAwait(true);
            if (info is not null)
            {
                return info;
            }
        }

        return new MediaInfo(null, false);
    }

    private async Task<MediaInfo?> TryFfprobeAsync(string path, CancellationToken ct)
    {
        try
        {
            var durProc = _launcher.Start(
                _ffprobePath!,
                ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                new ProcessOptions(Timeout: TimeSpan.FromSeconds(15)));
            var durationOutput = "";
            await foreach (var line in durProc.StdoutLines.ConfigureAwait(false))
            {
                durationOutput += line;
            }
            await durProc.WaitAsync(ct).ConfigureAwait(true);
            await durProc.DisposeAsync().ConfigureAwait(false);

            var streamProc = _launcher.Start(
                _ffprobePath!,
                ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_type", "-of", "csv=p=0", path],
                new ProcessOptions(Timeout: TimeSpan.FromSeconds(15)));
            var streamOutput = "";
            await foreach (var line in streamProc.StdoutLines.ConfigureAwait(false))
            {
                streamOutput += line;
            }
            await streamProc.WaitAsync(ct).ConfigureAwait(true);
            await streamProc.DisposeAsync().ConfigureAwait(false);

            var duration = ParseDuration(durationOutput);
            var hasVideo = streamOutput.Trim().Length > 0;
            return new MediaInfo(duration, hasVideo);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        return null;
    }

    private static TimeSpan? ParseDuration(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }
        if (double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return null;
    }
}
