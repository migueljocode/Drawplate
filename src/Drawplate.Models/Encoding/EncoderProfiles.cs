using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Drawplate.Models.Encoding;

/// <summary>Per-encoder arguments. Vectors: Bash/vneat (VAAPI, lines 738-768), PowerShell/vneat.ps1 (AMF, line 530). Not yet run on hardware (R20).</summary>
public static class EncoderProfiles
{
    public static EncoderProfile For(EncoderKind kind, int quality, VaapiTarget? vaapi = null)
    {
        var crf = Text(QualityScale.ToCrf(quality));
        return kind switch
        {
            EncoderKind.Cpu => Plain(["-c:v", "libx264", "-crf", crf, "-preset", "medium", "-pix_fmt", "yuv420p"]),
            EncoderKind.Nvenc => Plain(["-c:v", "h264_nvenc", "-rc", "vbr", "-cq", crf, "-b:v", "0", "-pix_fmt", "yuv420p"]),
            EncoderKind.Qsv => Plain(["-c:v", "h264_qsv", "-global_quality", crf, "-pix_fmt", "nv12"]),
            EncoderKind.VideoToolbox => Plain(["-c:v", "h264_videotoolbox", "-q:v", Text(quality), "-profile:v", "high", "-pix_fmt", "yuv420p"]),
            EncoderKind.Amf => Plain(["-c:v", "h264_amf", "-quality", "quality", "-rc", "cqp", "-qp_i", crf, "-qp_p", crf, "-pix_fmt", "yuv420p"]),
            EncoderKind.Vaapi when vaapi is not null => Vaapi(vaapi, crf),
            EncoderKind.Vaapi => throw new ArgumentNullException(nameof(vaapi)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown encoder kind."),
        };
    }

    private static EncoderProfile Plain(ImmutableArray<string> encode) =>
        new([], null, encode);

    private static EncoderProfile Vaapi(VaapiTarget target, string crf)
    {
        ImmutableArray<string> encode = ["-c:v", "h264_vaapi", "-global_quality", crf];
        if (target.LowPower)
        {
            encode = [.. encode, "-low_power", "1"];
        }

        return new EncoderProfile(["-vaapi_device", target.Device], "format=nv12,hwupload", encode);
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
