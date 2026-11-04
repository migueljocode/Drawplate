using System;

namespace Drawplate.Models.Formatting;

public static class HumanFormat
{
    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return $"{kb:0.0} KB";
        }

        var mb = kb / 1024.0;
        if (mb < 1024)
        {
            return $"{mb:0.0} MB";
        }

        var gb = mb / 1024.0;
        return $"{gb:0.0} GB";
    }

    public static string Time(TimeSpan time)
    {
        var totalSeconds = (int)time.TotalSeconds;
        if (totalSeconds >= 3600)
        {
            var h = totalSeconds / 3600;
            var m = (totalSeconds % 3600) / 60;
            var s = totalSeconds % 60;
            return $"{h}:{m:D2}:{s:D2}";
        }

        var minutes = totalSeconds / 60;
        var secs = totalSeconds % 60;
        return $"{minutes}:{secs:D2}";
    }

    public static string Clip(string? text, int width)
    {
        if (text is null)
        {
            return "";
        }

        if (text.Length <= width)
        {
            return text;
        }

        if (width <= 1)
        {
            return "…";
        }

        return text[..(width - 1)] + "…";
    }
}
