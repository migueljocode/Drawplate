using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Ffmpeg;

public sealed class EncoderProbe
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly bool[] LowPowerModes = [false, true];

    private readonly IProcessLauncher _launcher;
    private readonly OperatingSystemKind _os;
    private readonly TimeSpan _probeTimeout;
    private readonly Action<string>? _log;
    private readonly Func<string, IEnumerable<string>> _listDirectory;

    public EncoderProbe(
        IProcessLauncher launcher,
        FfmpegInfoService infoService,
        OperatingSystemKind os,
        TimeSpan? probeTimeout = null,
        Action<string>? log = null,
        Func<string, IEnumerable<string>>? listDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(infoService);
        _launcher = launcher;
        _os = os;
        _probeTimeout = probeTimeout ?? DefaultTimeout;
        _log = log;
        _listDirectory = listDirectory ?? VaapiDevices.ListDirectory;
    }

    public Task<ImmutableArray<ProbeResult>> ProbeAllAsync(FfmpegInfo info, CancellationToken ct) =>
        ProbeAsync(info, EncoderSets.For(_os), ct);

    /// <summary>Probes only <paramref name="kinds"/>. A kind FFmpeg does not list, or VAAPI without a render node, starts no process.</summary>
    public async Task<ImmutableArray<ProbeResult>> ProbeAsync(FfmpegInfo info, ImmutableArray<EncoderKind> kinds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(info);

        var results = ImmutableArray.CreateBuilder<ProbeResult>(kinds.Length);
        foreach (var kind in kinds)
        {
            results.Add(await ProbeKindAsync(info, kind, ct).ConfigureAwait(false));
        }

        return results.ToImmutable();
    }

    private static string FfmpegEncoderName(EncoderKind kind) => kind switch
    {
        EncoderKind.Cpu => "libx264",
        EncoderKind.Nvenc => "h264_nvenc",
        EncoderKind.Qsv => "h264_qsv",
        EncoderKind.Vaapi => "h264_vaapi",
        EncoderKind.Amf => "h264_amf",
        EncoderKind.VideoToolbox => "h264_videotoolbox",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown encoder kind."),
    };

    private static ProbeResult Failed(EncoderKind kind, ProbeFailureKind failure, string detail = "", VaapiTarget? vaapi = null) =>
        new(kind, false, failure, detail, vaapi);

    private static ProbeFailureKind MapFailureKind(FailureKind kind) => kind switch
    {
        FailureKind.InvalidInput => ProbeFailureKind.InvalidInput,
        FailureKind.EncoderUnavailable => ProbeFailureKind.EncoderUnavailable,
        FailureKind.NvencDriverTooOld => ProbeFailureKind.NvencDriverTooOld,
        FailureKind.DeviceAccess => ProbeFailureKind.DeviceAccess,
        _ => ProbeFailureKind.Unknown,
    };

    private async Task<ProbeResult> ProbeKindAsync(FfmpegInfo info, EncoderKind kind, CancellationToken ct)
    {
        if (!info.Encoders.Contains(FfmpegEncoderName(kind)))
        {
            return Failed(kind, kind == EncoderKind.Cpu ? ProbeFailureKind.EncoderUnavailable : ProbeFailureKind.NotListed);
        }

        return kind == EncoderKind.Vaapi
            ? await ProbeVaapiAsync(info, ct).ConfigureAwait(false)
            : await RunProbeAsync(info, kind, null, ct).ConfigureAwait(false);
    }

    /// <summary>Tries each render node, standard mode first and then low-power; the first pass wins.</summary>
    private async Task<ProbeResult> ProbeVaapiAsync(FfmpegInfo info, CancellationToken ct)
    {
        var nodes = VaapiDevices.Find(_listDirectory);
        if (nodes.IsEmpty)
        {
            return Failed(EncoderKind.Vaapi, ProbeFailureKind.NoDevice);
        }

        ProbeResult? last = null;
        foreach (var node in nodes)
        {
            foreach (var lowPower in LowPowerModes)
            {
                last = await RunProbeAsync(info, EncoderKind.Vaapi, new VaapiTarget(node, lowPower), ct).ConfigureAwait(false);
                if (last.Passed)
                {
                    return last;
                }
            }
        }

        return last!;
    }

    /// <summary>Runs one probe. Start failures and timeouts are failures; cancellation by the caller propagates.</summary>
    private async Task<ProbeResult> RunProbeAsync(FfmpegInfo info, EncoderKind kind, VaapiTarget? vaapi, CancellationToken ct)
    {
        var argv = FfmpegArgv.Probe(kind, vaapi).ToArray();
        _log?.Invoke($"probe {EncoderSets.Id(kind)}: {string.Join(' ', argv)}");

        IRunningProcess process;
        try
        {
            process = _launcher.Start(info.Path, argv, new ProcessOptions(_probeTimeout, Cancellation: ct));
        }
        catch (ProcessStartFailedException ex)
        {
            return LogFailure(Failed(kind, ProbeFailureKind.Unknown, ex.Message, vaapi));
        }

        try
        {
            await foreach (var _ in process.StdoutLines.ConfigureAwait(false))
            {
                // stdout is not used for probes
            }

            var exitCode = await process.WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var stderrTail = process.StderrTail;
            if (process.TimedOut)
            {
                return LogFailure(Failed(kind, ProbeFailureKind.Unknown, stderrTail, vaapi));
            }

            return exitCode == 0
                ? new ProbeResult(kind, true, null, stderrTail, vaapi)
                : LogFailure(Failed(kind, MapFailureKind(FailureClassifier.Classify(stderrTail)), stderrTail, vaapi));
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }

    private ProbeResult LogFailure(ProbeResult result)
    {
        _log?.Invoke($"probe {EncoderSets.Id(result.Kind)} failed: {result.StderrTail}");
        return result;
    }
}
