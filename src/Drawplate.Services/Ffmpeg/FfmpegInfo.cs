using System;
using System.Collections.Immutable;

using Drawplate.Models.Ffmpeg;

namespace Drawplate.Services.Ffmpeg;

public sealed record FfmpegInfo(
    string Path,
    FfmpegVersion Version,
    ImmutableHashSet<string> Encoders,
    bool HasLibx264);

public sealed record FfmpegNotRunnable(string Reason);

public sealed record FfmpegTooOld(Version Version)
{
    public string Reason => $"FFmpeg {Version} is too old";
}

/// <summary>Outcome of asking FFmpeg about itself: usable, not runnable, or too old.</summary>
public sealed record FfmpegInfoResult(FfmpegInfo? Info, FfmpegNotRunnable? NotRunnable, FfmpegTooOld? TooOld)
{
    public static FfmpegInfoResult Ok(FfmpegInfo info) => new(info, null, null);

    public static FfmpegInfoResult Broken(string reason) => new(null, new FfmpegNotRunnable(reason), null);

    public static FfmpegInfoResult Old(Version version) => new(null, null, new FfmpegTooOld(version));
}
