namespace Drawplate.Models.Encoding;

public static class FfmpegArgv
{
    public static ImmutableArray<string> Convert(string source, string partial, EncoderKind encoder, int quality, SizeCap cap)
    {
        return [
            "-nostdin", "-hide_banner", "-loglevel", "error", "-nostats", "-progress", "pipe:1",
            "-i", source, "-map", "0:v:0", "-map", "0:a:0?", "-sn", "-dn",
            "-vf", SizeCaps.VideoFilter(cap),
            .. EncoderProfiles.Args(encoder, quality),
            "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", "-f", "mp4", "-y", partial,
        ];
    }

    public static ImmutableArray<string> Probe(EncoderKind kind)
    {
        return [
            "-nostdin", "-hide_banner", "-loglevel", "quiet",
            "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.1",
            "-vf", SizeCaps.VideoFilter(SizeCap.None),
            .. EncoderProfiles.Args(kind, QualityScale.Default),
            "-frames:v", "1", "-f", "null", "-",
        ];
    }

    public static ImmutableArray<string> Banner(string source) =>
        ["-nostdin", "-hide_banner", "-i", source];
}
