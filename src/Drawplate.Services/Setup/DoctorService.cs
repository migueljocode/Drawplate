using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Setup;

public sealed record DoctorReport
{
    public required ImmutableArray<DoctorLine> Lines { get; init; }
    public required int ExitCode { get; init; }
    public Message? Error { get; init; }
}

public sealed record DoctorLine(string Status, string Check, string Detail);

public sealed class DoctorService
{
    private readonly FfmpegInfoService _infoService;
    private readonly IProcessLauncher _launcher;
    private readonly PlatformInfo _platform;
    private readonly Func<string?, FfmpegCandidate?> _locator;
    private readonly Func<string, string?> _ffprobeLocator;

    public DoctorService(
        FfmpegInfoService infoService,
        IProcessLauncher launcher,
        PlatformInfo platform,
        Func<string?, FfmpegCandidate?>? locator = null,
        Func<string, string?>? ffprobeLocator = null)
    {
        ArgumentNullException.ThrowIfNull(infoService);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(platform);
        _infoService = infoService;
        _launcher = launcher;
        _platform = platform;
        _locator = locator ?? LocateSystem;
        _ffprobeLocator = ffprobeLocator ?? LocateFfprobe;
    }

    public async Task<DoctorReport> RunAsync(string? ffmpegPath, CancellationToken ct)
    {
        var os = _platform.Os;
        var candidate = _locator(ffmpegPath);

        var lines = ImmutableArray.CreateBuilder<DoctorLine>();
        if (candidate is null)
        {
            lines.Add(new DoctorLine("missing", "FFmpeg", "not found"));
            return new DoctorReport
            {
                Lines = lines.ToImmutable(),
                ExitCode = 3,
                Error = null
            };
        }

        var result = await _infoService.GetInfoAsync(candidate.Path, ct).ConfigureAwait(false);
        if (result.Info is not { } info)
        {
            if (result.TooOld is { } tooOld)
            {
                lines.Add(new DoctorLine("outdated", "FFmpeg", $"{candidate.Path} ({tooOld.Version})"));
                return new DoctorReport
                {
                    Lines = lines.ToImmutable(),
                    ExitCode = 3,
                    Error = Messages.E_FF_07(tooOld.Version.ToString(), _platform.Os)
                };
            }

            var reason = result.NotRunnable?.Reason ?? "unknown";
            lines.Add(new DoctorLine("broken", "FFmpeg", $"{candidate.Path} ({reason})"));
            return new DoctorReport
            {
                Lines = lines.ToImmutable(),
                ExitCode = 3,
                Error = Messages.E_FF_02(candidate.Path, reason)
            };
        }

        lines.Add(new DoctorLine("ok", "FFmpeg", $"{info.Path} ({info.Version.Version}, found on {SourceName(candidate.Source)})"));
        lines.Add(new DoctorLine(info.HasLibx264 ? "ok" : "broken", "libx264", info.HasLibx264 ? "present" : "absent"));

        var ffprobe = _ffprobeLocator(candidate.Path);
        lines.Add(new DoctorLine("ok", "ffprobe", ffprobe is null ? "not found (optional; durations come from ffmpeg)" : $"{ffprobe} (optional)"));

        var probe = new EncoderProbe(_launcher, _infoService, os);
        var results = await probe.ProbeAsync(info, EncoderSets.For(os), ct).ConfigureAwait(false);
        var working = results.Where(r => r.Passed).Select(r => EncoderSets.Id(r.Kind)).ToImmutableArray();
        lines.Add(new DoctorLine(working.IsEmpty ? "broken" : "ok", "Encoders", working.IsEmpty ? "none" : string.Join(", ", working)));

        var hardwarePassed = results.Any(r => r.Passed && r.Kind != EncoderKind.Cpu);
        if (!info.HasLibx264 && !hardwarePassed)
        {
            return new DoctorReport
            {
                Lines = lines.ToImmutable(),
                ExitCode = 3,
                Error = Messages.E_FF_03(_platform.Os)
            };
        }

        var nvenc = results.FirstOrDefault(r => r.Kind == EncoderKind.Nvenc && r.FailureKind == ProbeFailureKind.NvencDriverTooOld);
        var minimumDriver = nvenc is null ? null : FailureClassifier.ParseNvenc(nvenc.StderrTail).MinimumDriver;
        var note = HardwareNote.For(
            GpuDetector.HasGpu(),
            hardwarePassed,
            nvenc is not null,
            minimumDriver);
        if (note is not null)
        {
            lines.Add(new DoctorLine("note", "Hardware", note.StartsWith("Note: ", StringComparison.Ordinal) ? note[6..] : note));
        }

        return new DoctorReport { Lines = lines.ToImmutable(), ExitCode = 0 };
    }

    private string? LocateFfprobe(string ffmpegPath) =>
        FfprobeLocator.Find(
            ffmpegPath,
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            _platform.Os);

    private FfmpegCandidate? LocateSystem(string? ffmpegPath)
    {
        var os = _platform.Os;
        return FfmpegLocator.Locate(
            ffmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(os, Environment.GetEnvironmentVariable),
            os);
    }

    private static string SourceName(FfmpegSource source) => source switch
    {
        FfmpegSource.Argument => "--ffmpeg-path",
        FfmpegSource.Environment => "DRAWPLATE_FFMPEG",
        FfmpegSource.Path => "PATH",
        FfmpegSource.KnownFolder => "known folder",
        _ => "unknown"
    };
}
