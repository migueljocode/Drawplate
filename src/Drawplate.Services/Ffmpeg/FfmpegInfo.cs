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
