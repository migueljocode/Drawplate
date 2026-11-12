using System;
using System.Collections.Generic;

namespace Drawplate.Console.Ui;

/// <summary>Draws width-safe rounded UTF-8 or crisp ASCII panels with Aurora gradient borders and titles.</summary>
internal static class PanelBuilder
{
    public static IReadOnlyList<string> Build(string title, IReadOnlyList<string> bodyLines, int terminalWidth, bool utf8, Rgb? borderColor = null, int frame = 0)
    {
        var maxOuter = Math.Max(28, terminalWidth - 2);
        var innerWidth = Math.Max(20, maxOuter - 4);
        var border = borderColor ?? Palette.BorderPrimary;
        var bFg = border.Fg();

        var tl = utf8 ? "╭" : "+";
        var tr = utf8 ? "╮" : "+";
        var bl = utf8 ? "╰" : "+";
        var br = utf8 ? "╯" : "+";
        var h = utf8 ? '─' : '-';
        var v = utf8 ? "│" : "|";

        var result = new List<string>(bodyLines.Count + 2);
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : $" {title.Trim()} ";
        var styledTitle = string.IsNullOrEmpty(cleanTitle)
            ? string.Empty
            : Gradient.Text(cleanTitle, Palette.AuroraBright, frame);

        var titleLen = cleanTitle.Length;
        var totalTopDashes = Math.Max(2, innerWidth + 2 - titleLen);
        var leftDashes = Math.Min(2, totalTopDashes);
        var rightDashes = Math.Max(0, totalTopDashes - leftDashes);

        var topLine = $"{bFg}{tl}{new string(h, leftDashes)}{Rgb.Reset}{styledTitle}{bFg}{new string(h, rightDashes)}{tr}{Rgb.Reset}";
        result.Add(AnsiText.ClipVisible(topLine, maxOuter));

        foreach (var line in bodyLines)
        {
            var clipped = AnsiText.ClipVisible(line, innerWidth);
            var padded = AnsiText.PadVisibleRight(clipped, innerWidth);
            result.Add($"{bFg}{v}{Rgb.Reset} {padded} {bFg}{v}{Rgb.Reset}");
        }

        var bottomLine = $"{bFg}{bl}{new string(h, innerWidth + 2)}{br}{Rgb.Reset}";
        result.Add(AnsiText.ClipVisible(bottomLine, maxOuter));
        return result;
    }

    public static string KeyValue(string label, string value, int labelWidth = 10)
    {
        var cleanLabel = label.PadRight(labelWidth);
        return $"{Palette.Cyan.Fg()}{cleanLabel}{Rgb.Reset}  {value}";
    }

    public static string KeyBadge(string key, string label) =>
        $"{Palette.KeyBg.Bg()}{Palette.KeyFg.Fg()} {key} {Rgb.Reset} {Palette.TextSecondary.Fg()}{label}{Rgb.Reset}";
}
