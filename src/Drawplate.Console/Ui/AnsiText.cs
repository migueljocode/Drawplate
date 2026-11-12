using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Drawplate.Console.Ui;

/// <summary>ANSI-aware text measurement, clipping, and wrapping so Windows/Unix terminals never overflow columns.</summary>
internal static partial class AnsiText
{
    [GeneratedRegex(@"\u001b\[[0-9;?]*[A-Za-z]")]
    private static partial Regex SgrPattern();

    public static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return SgrPattern().Replace(text, string.Empty);
    }

    public static int VisibleLength(string text) => Strip(text).Length;

    /// <summary>Clips <paramref name="line"/> to at most <paramref name="maxVisible"/> printable characters, preserving ANSI resets.</summary>
    public static string ClipVisible(string line, int maxVisible)
    {
        if (string.IsNullOrEmpty(line) || maxVisible <= 0)
        {
            return string.Empty;
        }

        if (VisibleLength(line) <= maxVisible)
        {
            return line;
        }

        var sb = new StringBuilder(line.Length + 8);
        var visible = 0;
        var hasEscape = false;
        var i = 0;
        while (i < line.Length && visible < maxVisible)
        {
            if (line[i] == '\u001b' && i + 1 < line.Length && line[i + 1] == '[')
            {
                var end = i + 2;
                while (end < line.Length && !IsEscapeTerminator(line[end]))
                {
                    end++;
                }

                if (end < line.Length)
                {
                    end++;
                }

                sb.Append(line, i, end - i);
                hasEscape = true;
                i = end;
                continue;
            }

            sb.Append(line[i]);
            visible++;
            i++;
        }

        if (hasEscape)
        {
            sb.Append(Rgb.Reset);
        }

        return sb.ToString();
    }

    public static string PadVisibleRight(string text, int totalWidth)
    {
        var visible = VisibleLength(text);
        if (visible >= totalWidth)
        {
            return ClipVisible(text, totalWidth);
        }

        return text + new string(' ', totalWidth - visible);
    }

    public static IReadOnlyList<string> WrapPlain(string text, int width)
    {
        var max = Math.Max(8, width);
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        var rest = text.TrimEnd();
        while (rest.Length > max)
        {
            var split = rest.LastIndexOf(' ', max);
            if (split < max / 3)
            {
                split = max;
            }

            lines.Add(rest[..split].TrimEnd());
            rest = rest[split..].TrimStart();
        }

        if (rest.Length > 0 || lines.Count == 0)
        {
            lines.Add(rest);
        }

        return lines;
    }

    private static bool IsEscapeTerminator(char ch) =>
        (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z');
}
