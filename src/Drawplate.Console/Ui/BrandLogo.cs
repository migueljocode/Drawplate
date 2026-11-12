using System;
using System.Collections.Generic;

using Drawplate.Console.Cli;

namespace Drawplate.Console.Ui;

/// <summary>Width-adaptive DRAWPLATE Aurora banner rendered in UTF-8 block art or crisp ASCII with top breathing margin.</summary>
internal static class BrandLogo
{
    public const int TopMargin = 1;

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
        @" ____  ____      ___        ______  _        _  _____ _____ ",
        @"|  _ \|  _ \    / \ \      / /  _ \| |      / \|_   _| ____|",
        @"| | | | |_) |  / _ \ \ /\ / /| |_) | |     / _ \ | | |  _|  ",
        @"| |_| |  _ <  / ___ \ V  V / |  __/| |___ / ___ \| | | |___ ",
        @"|____/|_| \_\/_/   \_\_/\_/  |_|   |_____/_/   \_\_| |_____|",
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

    public static IReadOnlyList<string> RenderLines(
        int width,
        bool utf8,
        int frame = 0,
        double reveal = 1.0,
        string? subtitle = null,
        int topMargin = 0) =>
        RenderLinesAt(width, utf8, frame * 0.032, reveal, subtitle, topMargin);

    public static IReadOnlyList<string> RenderLinesAt(
        int width,
        bool utf8,
        double timeSeconds,
        double reveal = 1.0,
        string? subtitle = null,
        int topMargin = TopMargin)
    {
        var usable = Math.Max(20, width - 2);
        var lines = new List<string>(10);
        for (var i = 0; i < topMargin; i++)
        {
            lines.Add(string.Empty);
        }

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
            var colored = Gradient.TextAt(art[row], Palette.Aurora, timeSeconds, reveal, row * 0.11);
            lines.Add(AnsiText.ClipVisible(pad + colored, usable));
        }

        var dot = utf8 ? "◆" : "*";
        var subText = subtitle ?? $"H.264 / AAC Video Transcoder  {(utf8 ? "·" : "-")}  v{VersionInfo.Current}";
        var fullSub = $"{dot} {subText}";
        var subPad = Math.Max(0, (usable - fullSub.Length) / 2);
        var accent = Gradient.Wave(Palette.AuroraBright, 0.4, timeSeconds * 0.25);
        var styledSub = $"{accent.Fg()}{dot}{Rgb.Reset} {Palette.TextSecondary.Fg()}{subText}{Rgb.Reset}";
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
