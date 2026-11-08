using System.Collections.Immutable;

namespace Drawplate.Models.Encoding;

public static class FfmpegArgv
{
    public static ImmutableArray<string> Convert(string source, string partial, EncoderKind encoder, int quality, SizeCap cap, VaapiTarget? vaapi = null)
    {
        var profile = EncoderProfiles.For(encoder, quality, vaapi);
        return [
            "-nostdin", "-hide_banner", "-loglevel", "error", "-nostats", "-progress", "pipe:1",
            .. profile.PreInput,
            "-i", source, "-map", "0:v:0", "-map", "0:a:0?", "-sn", "-dn",
            "-vf", WithSuffix(SizeCaps.VideoFilter(cap), profile.FilterSuffix),
            .. profile.Encode,
            "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", "-f", "mp4", "-y", partial,
        ];
    }

    public static ImmutableArray<string> Probe(EncoderKind kind, VaapiTarget? vaapi = null)
    {
        var profile = EncoderProfiles.For(kind, QualityScale.Default, vaapi);
        return [
            "-nostdin", "-hide_banner", "-loglevel", "quiet",
            .. profile.PreInput,
            "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.1",
            "-vf", WithSuffix(SizeCaps.VideoFilter(SizeCap.None), profile.FilterSuffix),
            .. profile.Encode,
            "-frames:v", "1", "-f", "null", "-",
        ];
    }

    public static ImmutableArray<string> Banner(string source) =>
        ["-nostdin", "-hide_banner", "-i", source];

    private static string WithSuffix(string filter, string? suffix) =>
        suffix is null ? filter : filter + "," + suffix;
}
