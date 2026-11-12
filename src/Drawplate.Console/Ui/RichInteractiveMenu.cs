using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Services.Interaction;

namespace Drawplate.Console.Ui;

/// <summary>Arrow-key Aurora TUI wizard for <c>drawplate -m</c> on interactive color terminals.</summary>
internal static class RichInteractiveMenu
{
    private static readonly (int Quality, string Label)[] QualityPresets =
    [
        (30, "Compact    (CRF 33 · smallest files)"),
        (50, "Balanced   (CRF 26 · default)"),
        (75, "High       (CRF 15 · crisp detail)"),
        (90, "Archival   (CRF 6  · near-lossless)"),
    ];

    private static readonly (SizeCap Cap, string Label)[] SizeOptions =
    [
        (SizeCap.None, "none       (keep original resolution)"),
        (SizeCap.Sd, "sd         (max 854x480)"),
        (SizeCap.Hd, "hd         (max 1280x720)"),
        (SizeCap.FullHd, "fullhd     (max 1920x1080)"),
        (SizeCap.Uhd4K, "4k         (max 3840x2160)"),
    ];

    public static MenuResult Run(TextWriter stdout, Theme theme, ImmutableArray<EncoderKind> workingEncoders, int currentQuality, SizeCap currentMaxSize)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        using var consoleGuard = ConsoleGuard.EnterInteractive();
        var ordered = workingEncoders.Distinct().ToImmutableArray();
        var encoderChoices = new List<(EncoderKind? Kind, string Label)>
        {
            (null, "auto       (best hardware encoder, fallback to CPU)"),
        };
        foreach (var kind in ordered)
        {
            var id = EncoderSets.Id(kind);
            var desc = kind == EncoderKind.Cpu ? "software libx264 (universal)" : $"hardware {EncoderSets.Badge(kind)}";
            encoderChoices.Add((kind, $"{id.PadRight(10)} ({desc})"));
        }

        var selectedEncoderIdx = SelectOption(stdout, theme, "STEP 1/3 · SELECT ENCODER", encoderChoices.Select(c => c.Label).ToList(), 0);
        if (selectedEncoderIdx < 0)
        {
            return Cancelled();
        }

        var selectedQuality = SelectQuality(stdout, theme, currentQuality);
        if (selectedQuality < 0)
        {
            return Cancelled();
        }

        var defaultSizeIdx = Array.FindIndex(SizeOptions, s => s.Cap == currentMaxSize);
        var selectedSizeIdx = SelectOption(stdout, theme, "STEP 3/3 · SELECT MAX RESOLUTION", SizeOptions.Select(s => s.Label).ToList(), Math.Max(0, defaultSizeIdx));
        if (selectedSizeIdx < 0)
        {
            return Cancelled();
        }

        return new MenuResult
        {
            Encoder = encoderChoices[selectedEncoderIdx].Kind,
            Quality = selectedQuality,
            MaxSize = SizeOptions[selectedSizeIdx].Cap,
            Error = null,
        };
    }

    private static int SelectOption(TextWriter stdout, Theme theme, string title, List<string> items, int initialIndex)
    {
        var index = Math.Clamp(initialIndex, 0, Math.Max(0, items.Count - 1));
        var pointer = theme.Utf8Glyphs ? "▸" : ">";
        var drawn = 0;

        stdout.Write("\u001b[?25l");
        try
        {
            while (true)
            {
                var body = new List<string>(items.Count + 2);
                for (var i = 0; i < items.Count; i++)
                {
                    if (i == index)
                    {
                        body.Add($"{Palette.Emerald.Fg()}{pointer} {Palette.TextPrimary.Fg()}{items[i]}{Rgb.Reset}");
                    }
                    else
                    {
                        body.Add($"  {Palette.TextSecondary.Fg()}{items[i]}{Rgb.Reset}");
                    }
                }

                body.Add(string.Empty);
                body.Add($"{PanelBuilder.KeyBadge("↑/↓", "move")}  {PanelBuilder.KeyBadge("Enter", "confirm")}  {PanelBuilder.KeyBadge("Esc", "cancel")}");

                var panel = PanelBuilder.Build(title, body, theme.Width, theme.Utf8Glyphs, Palette.BorderPrimary);
                RedrawBlock(stdout, panel, ref drawn);

                var key = global::System.Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.UpArrow || key.KeyChar == 'k')
                {
                    index = (index - 1 + items.Count) % items.Count;
                }
                else if (key.Key is ConsoleKey.DownArrow || key.KeyChar == 'j')
                {
                    index = (index + 1) % items.Count;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    ClearBlock(stdout, drawn);
                    return index;
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
                {
                    ClearBlock(stdout, drawn);
                    return -1;
                }
            }
        }
        finally
        {
            stdout.Write("\u001b[?25h");
            stdout.Flush();
        }
    }

    private static int SelectQuality(TextWriter stdout, Theme theme, int currentQuality)
    {
        var quality = Math.Clamp(currentQuality, 1, 100);
        var drawn = 0;
        var pointer = theme.Utf8Glyphs ? "▸" : ">";

        stdout.Write("\u001b[?25l");
        try
        {
            while (true)
            {
                var crf = QualityScale.ToCrf(quality);
                var bar = Gradient.Bar(28, quality / 100.0, Palette.Aurora, frame: 0, utf8: theme.Utf8Glyphs);
                var body = new List<string>
                {
                    $"{Palette.Cyan.Fg()}Quality:{Rgb.Reset} {Palette.TextPrimary.Fg()}{quality.ToString(CultureInfo.InvariantCulture)}/100{Rgb.Reset}  {Palette.TextMuted.Fg()}(CRF {crf.ToString(CultureInfo.InvariantCulture)}){Rgb.Reset}",
                    bar,
                    string.Empty,
                };

                foreach (var (presetVal, label) in QualityPresets)
                {
                    var active = presetVal == quality;
                    body.Add(active
                        ? $"{Palette.Emerald.Fg()}{pointer} {Palette.TextPrimary.Fg()}{label}{Rgb.Reset}"
                        : $"  {Palette.TextSecondary.Fg()}{label}{Rgb.Reset}");
                }

                body.Add(string.Empty);
                body.Add($"{PanelBuilder.KeyBadge("←/→", "-/+ 5")}  {PanelBuilder.KeyBadge("↑/↓", "preset")}  {PanelBuilder.KeyBadge("Enter", "confirm")}  {PanelBuilder.KeyBadge("Esc", "cancel")}");

                var panel = PanelBuilder.Build("STEP 2/3 · SELECT QUALITY", body, theme.Width, theme.Utf8Glyphs, Palette.BorderPrimary);
                RedrawBlock(stdout, panel, ref drawn);

                var key = global::System.Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.LeftArrow || key.KeyChar == 'h')
                {
                    quality = Math.Max(1, quality - 5);
                }
                else if (key.Key is ConsoleKey.RightArrow || key.KeyChar == 'l')
                {
                    quality = Math.Min(100, quality + 5);
                }
                else if (key.Key is ConsoleKey.UpArrow || key.KeyChar == 'k')
                {
                    quality = PreviousPreset(quality);
                }
                else if (key.Key is ConsoleKey.DownArrow || key.KeyChar == 'j')
                {
                    quality = NextPreset(quality);
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    ClearBlock(stdout, drawn);
                    return quality;
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
                {
                    ClearBlock(stdout, drawn);
                    return -1;
                }
            }
        }
        finally
        {
            stdout.Write("\u001b[?25h");
            stdout.Flush();
        }
    }

    private static int NextPreset(int current)
    {
        foreach (var (val, _) in QualityPresets)
        {
            if (val > current)
            {
                return val;
            }
        }

        return QualityPresets[0].Quality;
    }

    private static int PreviousPreset(int current)
    {
        for (var i = QualityPresets.Length - 1; i >= 0; i--)
        {
            if (QualityPresets[i].Quality < current)
            {
                return QualityPresets[i].Quality;
            }
        }

        return QualityPresets[^1].Quality;
    }

    private static void RedrawBlock(TextWriter stdout, IReadOnlyList<string> lines, ref int previouslyDrawn)
    {
        if (previouslyDrawn > 0)
        {
            stdout.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b[{previouslyDrawn}A\r"));
        }

        foreach (var line in lines)
        {
            stdout.Write(line);
            stdout.Write("\u001b[K\n");
        }

        stdout.Flush();
        previouslyDrawn = lines.Count;
    }

    private static void ClearBlock(TextWriter stdout, int drawn)
    {
        if (drawn <= 0)
        {
            return;
        }

        stdout.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b[{drawn}A\r"));
        for (var i = 0; i < drawn; i++)
        {
            stdout.Write("\u001b[2K\n");
        }

        stdout.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b[{drawn}A\r"));
        stdout.Flush();
    }

    private static MenuResult Cancelled() =>
        new() { Encoder = null, Quality = null, MaxSize = null, Error = Messages.N_INT() };
}
