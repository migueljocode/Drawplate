using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Ffmpeg;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

public sealed class FfmpegInfoService
{
    private readonly IProcessLauncher _launcher;

    public FfmpegInfoService(IProcessLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
    }

    public async Task<FfmpegInfo?> GetInfoAsync(
        string ffmpegPath,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);

        try
        {
            return await RunVersionAsync(ffmpegPath, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<FfmpegInfo?> RunVersionAsync(string ffmpegPath, CancellationToken ct)
    {
        var process = _launcher.Start(ffmpegPath, ["-version"], new ProcessOptions(Timeout: TimeSpan.FromSeconds(10)));
        try
        {
            var versionOutput = "";
            await foreach (var line in process.StdoutLines.ConfigureAwait(false))
            {
                versionOutput += line + "\n";
            }

            var exitCode = await process.WaitAsync(ct).ConfigureAwait(true);
            if (exitCode != 0)
            {
                return null;
            }

            var version = FfmpegBannerParser.ParseVersion(versionOutput);
            if (version.Version < new Version(6, 0) && !version.IsMaster)
            {
                return null;
            }

            var encodersOutput = await RunEncodersAsync(ffmpegPath, ct).ConfigureAwait(true);
            var encoders = FfmpegBannerParser.ParseEncoders(encodersOutput);

            return new FfmpegInfo(
                ffmpegPath,
                version,
                encoders,
                encoders.Contains("libx264"));
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<string> RunEncodersAsync(string ffmpegPath, CancellationToken ct)
    {
        var process = _launcher.Start(ffmpegPath, ["-encoders"], new ProcessOptions(Timeout: TimeSpan.FromSeconds(10)));
        try
        {
            var output = "";
            await foreach (var line in process.StdoutLines.ConfigureAwait(false))
            {
                output += line + "\n";
            }
            await process.WaitAsync(ct).ConfigureAwait(true);
            return output;
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }
}
