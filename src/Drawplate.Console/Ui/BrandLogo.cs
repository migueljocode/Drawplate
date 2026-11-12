using System;
using System.Collections.Generic;

using Drawplate.Console.Cli;

namespace Drawplate.Console.Ui;

/// <summary>Width-adaptive DRAWPLATE Aurora banner rendered in UTF-8 block art or crisp ASCII.</summary>
internal static class BrandLogo
{
    private static readonly string[] WideUtf8 =
    [
        "██████╗ ██████╗  █████╗ ██╗    ██╗██████╗ ██╗      █████╗ ████████╗███████╗",
        "██╔══██╗██╔══██╗██╔══██╗██║    ██║██╔══██╗██║     ██╔══██╗╚══██╔══╝██╔════╝",
        "██║  ██║██████╔╝███████║██║ █╗ ██║██████╔╝██║     ███████║   ██║   █████╗  ",
        "██║  ██║██╔══██╗██╔══██║██║███╗██║██╔═══╝ ██║     ██╔══██║   ██║   ██╔══╝  ",
        "██████╔╝██║  ██║██║  ██║╚███╔███╔╝██║     ███████╗██║  ██║   ██║   ███████╗",
        "╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝ ╚══╝╚══╝ ╚═╝     ╚══════╝╚═╝  ╚═╝   ╚═╝   ╚══════╝",
    ];

    private static readonly string[] WideAscii =
    [
        " ____  ____      ___        ______  _        _  _____ _____ ",
        "|  _ \\|  _ \\    / \\ \\      / /  _ \\| |      / \\|_   _| ____|",
        "| | | | |_) |  / _ \\ \\ /\\ / /| |_) | |     / _ \\ | | |  _|  ",
        "| |_| |  _ <  / ___ \\ V  V / |  __/| |___ / ___ \\| | | |___ ",
        "|____/|_| \\_\\/_/   \\_\\_/\\_/  |_|   |_____/_/   \\_\\_| |_____|",
    ];

    private static readonly string[] MediumUtf8 =
    [
        "█▀▄ █▀▄ ▄▀▄ █ █ █▀▄ █   ▄▀▄ ▀█▀ █▀▀",
        "█▄▀ █▀▄ █▀█ █▄█ █▀▀ █▄▄ █▀█  █  ██▄",
    ];

    private static readonly string[] MediumAscii =
    [
        "== DRAWPLATE ==",
    ];

    public static IReadOnlyList<string> RenderLines(int width, bool utf8, int frame = 0, double reveal = 1.0, string? subtitle = null)
    {
        var usable = Math.Max(20, width - 2);
        var lines = new List<string>();
        var art = SelectArt(usable, utf8);
        var maxArtWidth = 0;
        foreach (var line in art)
        {
            if (line.Length > maxArtWidth)
            {
                maxArtWidth = line.Length;
            }
        }

        var leftPad = Math.Max(0, (usable - maxArtWidth) / 2);
        var pad = new string(' ', leftPad);
        for (var row = 0; row < art.Length; row++)
        {
            var colored = Gradient.Text(art[row], Palette.Aurora, frame, reveal, row * 0.09);
            lines.Add(AnsiText.ClipVisible(pad + colored, usable));
        }

        var sub = subtitle ?? $"H.264 / AAC Video Transcoder  {(utf8 ? "·" : "-")}  v{VersionInfo.Current}";
        var subPad = Math.Max(0, (usable - sub.Length) / 2);
        var styledSub = $"{Palette.TextSecondary.Fg()}{sub}{Rgb.Reset}";
        lines.Add(AnsiText.ClipVisible(new string(' ', subPad) + styledSub, usable));
        return lines;
    }

    private static string[] SelectArt(int usableWidth, bool utf8)
    {
        if (utf8)
        {
            if (usableWidth >= 78)
            {
                return WideUtf8;
            }

            if (usableWidth >= 38)
            {
                return MediumUtf8;
            }

            return ["◆ DRAWPLATE"];
        }

        if (usableWidth >= 62)
        {
            return WideAscii;
        }

        if (usableWidth >= 18)
        {
            return MediumAscii;
        }

        return ["DRAWPLATE"];
    }
}
