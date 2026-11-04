using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

public sealed class EncoderProbe
{
    private readonly IProcessLauncher _launcher;
    private readonly FfmpegInfoService _infoService;
    private readonly OperatingSystemKind _os;

    public EncoderProbe(IProcessLauncher launcher, FfmpegInfoService infoService, OperatingSystemKind os)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(infoService);
        _launcher = launcher;
        _infoService = infoService;
        _os = os;
    }

    public async Task<ImmutableArray<ProbeResult>> ProbeAllAsync(
        FfmpegInfo info,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(info);
        var results = ImmutableArray.CreateBuilder<ProbeResult>();

        foreach (var kind in EncoderSets.For(_os))
        {
            var result = await ProbeOneAsync(info, kind, ct).ConfigureAwait(true);
            results.Add(result);
        }

        return results.ToImmutable();
    }

    private async Task<ProbeResult> ProbeOneAsync(
        FfmpegInfo info,
        EncoderKind kind,
        CancellationToken ct)
    {
        if (kind == EncoderKind.Cpu && !info.HasLibx264)
        {
            return new ProbeResult(EncoderKind.Cpu, false, ProbeFailureKind.Unknown, "");
        }

        var process = _launcher.Start(
            info.Path,
            FfmpegArgv.Probe(kind).ToArray(),
            new ProcessOptions(Timeout: TimeSpan.FromSeconds(15)));

        await foreach (var _ in process.StdoutLines.ConfigureAwait(false))
        {
            // stdout is not used for probe
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

        if (exitCode == 0)
        {
            return new ProbeResult(kind, true, null, stderrTail);
        }

        var failureKind = MapFailureKind(FailureClassifier.Classify(stderrTail));
        return new ProbeResult(kind, false, failureKind, stderrTail);
    }

    private static ProbeFailureKind MapFailureKind(FailureKind kind) => kind switch
    {
        FailureKind.InvalidInput => ProbeFailureKind.InvalidInput,
        FailureKind.EncoderUnavailable => ProbeFailureKind.EncoderUnavailable,
        FailureKind.NvencDriverTooOld => ProbeFailureKind.NvencDriverTooOld,
        FailureKind.DeviceAccess => ProbeFailureKind.DeviceAccess,
        _ => ProbeFailureKind.Unknown,
    };
}
