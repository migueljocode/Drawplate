namespace Drawplate.Models.Encoding;

public static class SizeCaps
{
    public static bool TryParse(string? text, out SizeCap cap)
    {
        cap = SizeCap.None;
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (string.Equals(text, "sd", System.StringComparison.OrdinalIgnoreCase))
        {
            cap = SizeCap.Sd;
            return true;
        }

        if (string.Equals(text, "hd", System.StringComparison.OrdinalIgnoreCase))
        {
            cap = SizeCap.Hd;
            return true;
        }

        if (string.Equals(text, "fullhd", System.StringComparison.OrdinalIgnoreCase))
        {
            cap = SizeCap.FullHd;
            return true;
        }

        if (string.Equals(text, "4k", System.StringComparison.OrdinalIgnoreCase))
        {
            cap = SizeCap.Uhd4K;
            return true;
        }

        if (string.Equals(text, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            cap = SizeCap.None;
            return true;
        }

        return false;
    }

    public static (int Width, int Height) Box(SizeCap cap) => cap switch
    {
        SizeCap.Sd => (854, 480),
        SizeCap.Hd => (1280, 720),
        SizeCap.FullHd => (1920, 1080),
        SizeCap.Uhd4K => (3840, 2160),
        _ => (0, 0),
    };

    public static string VideoFilter(SizeCap cap)
    {
        var (w, h) = Box(cap);
        if (cap == SizeCap.None)
        {
            return "scale=trunc(iw/2)*2:trunc(ih/2)*2";
        }

        return
            $"scale=w='if(gt(a,1),min(iw,{w}),min(iw,{h}))':h='if(gt(a,1),min(ih,{h}),min(ih,{w}))':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2";
    }
}
