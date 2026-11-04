namespace Drawplate.Models.Encoding;

public static class EncoderProfiles
{
    public static ImmutableArray<string> Args(EncoderKind kind, int quality) => kind switch
    {
        EncoderKind.Cpu => ["-c:v", "libx264", "-crf", QualityScale.ToCrf(quality).ToString(System.Globalization.CultureInfo.InvariantCulture), "-preset", "medium", "-pix_fmt", "yuv420p"],
        EncoderKind.Nvenc => ["-c:v", "h264_nvenc", "-rc", "vbr", "-cq", QualityScale.ToCrf(quality).ToString(System.Globalization.CultureInfo.InvariantCulture), "-b:v", "0", "-pix_fmt", "yuv420p"],
        EncoderKind.Qsv => ["-c:v", "h264_qsv", "-global_quality", QualityScale.ToCrf(quality).ToString(System.Globalization.CultureInfo.InvariantCulture), "-pix_fmt", "nv12"],
        EncoderKind.VideoToolbox => ["-c:v", "h264_videotoolbox", "-q:v", quality.ToString(System.Globalization.CultureInfo.InvariantCulture), "-profile:v", "high", "-pix_fmt", "yuv420p"],
        _ => throw new System.NotSupportedException("capture the golden vector first (T6.7)"),
    };
}
