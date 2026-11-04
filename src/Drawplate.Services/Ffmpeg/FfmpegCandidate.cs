namespace Drawplate.Services.Ffmpeg;

public enum FfmpegSource
{
    Argument,
    Environment,
    Path,
    KnownFolder
}

public sealed record FfmpegCandidate(string Path, FfmpegSource Source);
