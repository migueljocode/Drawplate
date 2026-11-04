using System.Text;

namespace Drawplate.Models.Formatting;

public static class Sanitizer
{
    public static string ForDisplay(string? text)
    {
        if (text is null)
        {
            return "";
        }

        var result = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var code = (ushort)ch;
            // Remove C0 controls (0x00-0x1F), DEL (0x7F), C1 controls (0x80-0x9F)
            if (code <= 0x1F || code == 0x7F || (code >= 0x80 && code <= 0x9F))
            {
                continue;
            }
            result.Append(ch);
        }

        return result.ToString();
    }
}
