namespace Drawplate.Models.Encoding;

public enum ParseStatus
{
    Ok,
    UnknownEverywhere,
    WrongOs
}

public sealed record EncoderParseResult
{
    public ParseStatus Status { get; init; }
    public EncoderKind? Kind { get; init; }
    public bool IsOk => Status == ParseStatus.Ok;

    public static EncoderParseResult Ok(EncoderKind kind) => new() { Status = ParseStatus.Ok, Kind = kind };
    public static EncoderParseResult UnknownEverywhere() => new() { Status = ParseStatus.UnknownEverywhere };
    public static EncoderParseResult WrongOs() => new() { Status = ParseStatus.WrongOs };
}

public static class EncoderSets
{
    public static ImmutableArray<EncoderKind> For(OperatingSystemKind os) => os switch
    {
        OperatingSystemKind.Linux => [EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Vaapi, EncoderKind.Cpu],
        OperatingSystemKind.Windows => [EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Amf, EncoderKind.Cpu],
        OperatingSystemKind.MacOS => [EncoderKind.VideoToolbox, EncoderKind.Cpu],
        _ => [],
    };

    public static string Id(EncoderKind kind) => kind switch
    {
        EncoderKind.Cpu => "cpu",
        EncoderKind.Nvenc => "nvenc",
        EncoderKind.Qsv => "qsv",
        EncoderKind.Vaapi => "vaapi",
        EncoderKind.Amf => "amf",
        EncoderKind.VideoToolbox => "videotoolbox",
        _ => "",
    };

    public static string Badge(EncoderKind kind) => kind switch
    {
        EncoderKind.Cpu => "[CPU]",
        EncoderKind.Nvenc => "[NVENC]",
        EncoderKind.Qsv => "[QSV]",
        EncoderKind.Vaapi => "[VAAPI]",
        EncoderKind.Amf => "[AMF]",
        EncoderKind.VideoToolbox => "[VT]",
        _ => "",
    };

    public static EncoderParseResult Parse(string text, OperatingSystemKind os)
    {
        EncoderKind? kind = text switch
        {
            "cpu" => EncoderKind.Cpu,
            "nvenc" => EncoderKind.Nvenc,
            "qsv" => EncoderKind.Qsv,
            "vaapi" => EncoderKind.Vaapi,
            "amf" => EncoderKind.Amf,
            "videotoolbox" => EncoderKind.VideoToolbox,
            _ => null,
        };

        if (kind is null)
        {
            return EncoderParseResult.UnknownEverywhere();
        }

        var accepted = For(os);
        if (!accepted.Contains(kind.Value))
        {
            return EncoderParseResult.WrongOs();
        }

        return EncoderParseResult.Ok(kind.Value);
    }
}
