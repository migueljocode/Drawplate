using System;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Platform;

namespace Drawplate.Services.Setup;

public sealed record DoctorReport
{
    public required ImmutableArray<DoctorLine> Lines { get; init; }
    public required int ExitCode { get; init; }
}

public sealed record DoctorLine(string Status, string Check, string Detail);

public sealed class DoctorService
{
    private readonly FfmpegInfoService _infoService;

    public DoctorService(FfmpegInfoService infoService)
    {
        ArgumentNullException.ThrowIfNull(infoService);
        _infoService = infoService;
    }

    public DoctorReport Run(string? ffmpegPath)
    {
        var os = PlatformDetector.DetectOs();
        var candidate = FfmpegLocator.Locate(
            ffmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(os, Environment.GetEnvironmentVariable),
            os);

        var lines = ImmutableArray.CreateBuilder<DoctorLine>();

        if (candidate is null)
        {
            lines.Add(new DoctorLine("missing", "FFmpeg", "not found"));
            return new DoctorReport { Lines = lines.ToImmutable(), ExitCode = 3 };
        }

        var result = _infoService.GetInfoAsync(candidate.Path, CancellationToken.None).GetAwaiter().GetResult();
        if (result.Info is not { } info)
        {
            var reason = result.NotRunnable?.Reason ?? result.TooOld?.Reason ?? "unknown";
            lines.Add(new DoctorLine("broken", "FFmpeg", $"{candidate.Path} ({reason})"));
            return new DoctorReport { Lines = lines.ToImmutable(), ExitCode = 3 };
        }

        var versionText = $"{info.Path} ({info.Version.Version}, found on PATH)";
        lines.Add(new DoctorLine("ok", "FFmpeg", versionText));
        lines.Add(new DoctorLine(info.HasLibx264 ? "ok" : "broken", "libx264", info.HasLibx264 ? "present" : "absent"));
        lines.Add(new DoctorLine("ok", "ffprobe", "not found (optional; durations come from ffmpeg)"));
        var encoders = string.Join(", ", info.Encoders.OrderBy(e => e));
        lines.Add(new DoctorLine("ok", "Encoders", encoders));

        return new DoctorReport { Lines = lines.ToImmutable(), ExitCode = 0 };
    }
}
