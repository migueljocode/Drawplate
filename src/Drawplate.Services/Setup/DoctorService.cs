using System;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;

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
        var pathVar = Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG");
        var pathFolders = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        var knownFolders = new[] { "/opt/homebrew/bin", "/usr/local/bin", "/home/linuxbrew/.linuxbrew/bin", "/usr/bin" };

        var candidate = FfmpegLocator.Locate(
            ffmpegPath,
            pathVar,
            pathFolders,
            File.Exists,
            Directory.GetDirectories,
            knownFolders);

        var lines = ImmutableArray.CreateBuilder<DoctorLine>();

        if (candidate is null)
        {
            lines.Add(new DoctorLine("missing", "FFmpeg", "not found"));
            return new DoctorReport { Lines = lines.ToImmutable(), ExitCode = 3 };
        }

        var info = _infoService.GetInfoAsync(candidate.Path, CancellationToken.None).GetAwaiter().GetResult();
        if (info is null)
        {
            lines.Add(new DoctorLine("broken", "FFmpeg", candidate.Path));
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
