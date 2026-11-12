using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Services.Interaction;

namespace Drawplate.Console.Ui;

/// <summary>Full-screen Aurora interactive wizard for <c>drawplate -m</c> with 45fps logo animation and mouse/arrow navigation.</summary>
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

    public static MenuResult Run(
        TextWriter stdout,
        Theme theme,
        ImmutableArray<EncoderKind> workingEncoders,
        int currentQuality,
        SizeCap currentMaxSize,
        Stopwatch? sharedClock = null)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        using var ownScreen = sharedClock is null
            ? ScreenGuard.TryEnter(stdout, theme, enableMouse: true)
            : null;
        var clock = sharedClock ?? Stopwatch.StartNew();

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

        var selectedEncoderIdx = SelectOption(stdout, theme, clock, "STEP 1/3 · SELECT ENCODER", encoderChoices.Select(c => c.Label).ToList(), 0);
        if (selectedEncoderIdx < 0)
        {
            return Cancelled();
        }

        var selectedQuality = SelectQuality(stdout, theme, clock, currentQuality);
        if (selectedQuality < 0)
        {
            return Cancelled();
        }

        var defaultSizeIdx = Array.FindIndex(SizeOptions, s => s.Cap == currentMaxSize);
        var selectedSizeIdx = SelectOption(stdout, theme, clock, "STEP 3/3 · SELECT MAX RESOLUTION", SizeOptions.Select(s => s.Label).ToList(), Math.Max(0, defaultSizeIdx));
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

    private static int SelectOption(TextWriter stdout, Theme theme, Stopwatch clock, string title, List<string> items, int initialIndex)
    {
        var index = Math.Clamp(initialIndex, 0, Math.Max(0, items.Count - 1));
        var pointer = theme.Utf8Glyphs ? "▸" : ">";

        while (true)
        {
            var (width, height) = HelpPager.SafeWindowSize(theme.Width);
            var elapsed = clock.Elapsed.TotalSeconds;
            var reveal = Math.Clamp((elapsed + 0.08) / 0.32, 0.25, 1.0);
            var headerLines = BrandLogo.RenderLinesAt(width, theme.Utf8Glyphs, elapsed, reveal, subtitle: "Interactive Configuration", topMargin: BrandLogo.TopMargin);

            var rows = new List<string>(items.Count + 2);
            for (var i = 0; i < items.Count; i++)
            {
                if (i == index)
                {
                    var badge = Gradient.Wave(Palette.AuroraBright, elapsed * 0.35, 0.0);
                    rows.Add($"{badge.Fg()}{pointer}{Rgb.Reset} {Palette.TextPrimary.Fg()}{items[i]}{Rgb.Reset}");
                }
                else
                {
                    rows.Add($"  {Palette.TextSecondary.Fg()}{items[i]}{Rgb.Reset}");
                }
            }

            var panel = PanelBuilder.Build(title, rows, Math.Min(width, 96), theme.Utf8Glyphs, Palette.BorderPrimary);
            DrawFullScreenMenu(stdout, headerLines, panel, width, height, theme.Utf8Glyphs, "↑/↓ or wheel move  ·  Enter confirm  ·  q / Esc cancel");

            for (var poll = 0; poll < 2; poll++)
            {
                if (TerminalInput.KeyAvailableSafe())
                {
                    var ev = TerminalInput.ReadEvent();
                    if (ev is null)
                    {
                        return -1;
                    }

                    switch (ev.Value.Action)
                    {
                        case UiKeyAction.Up or UiKeyAction.WheelUp:
                            index = (index - 1 + items.Count) % items.Count;
                            break;
                        case UiKeyAction.Down or UiKeyAction.WheelDown:
                            index = (index + 1) % items.Count;
                            break;
                        case UiKeyAction.Home:
                            index = 0;
                            break;
                        case UiKeyAction.End:
                            index = items.Count - 1;
                            break;
                        case UiKeyAction.Enter:
                            return index;
                        case UiKeyAction.Quit:
                            return -1;
                    }

                    break;
                }

                Thread.Sleep(11);
            }
        }
    }

    private static int SelectQuality(TextWriter stdout, Theme theme, Stopwatch clock, int initialQuality)
    {
        var quality = Math.Clamp(initialQuality, 1, 100);
        var pointer = theme.Utf8Glyphs ? "▸" : ">";

        while (true)
        {
            var (width, height) = HelpPager.SafeWindowSize(theme.Width);
            var elapsed = clock.Elapsed.TotalSeconds;
            var reveal = Math.Clamp((elapsed + 0.08) / 0.32, 0.25, 1.0);
            var headerLines = BrandLogo.RenderLinesAt(width, theme.Utf8Glyphs, elapsed, reveal, subtitle: "Interactive Configuration", topMargin: BrandLogo.TopMargin);

            var crf = QualityScale.ToCrf(quality);
            var barWidth = Math.Min(38, Math.Max(18, width - 32));
            var bar = Gradient.BarAt(barWidth, quality / 100.0, Palette.Aurora, elapsed, utf8: theme.Utf8Glyphs);
            var rows = new List<string>
            {
                $"{Palette.TextSecondary.Fg()}Current{Rgb.Reset}   {bar}  {Palette.Cyan.Fg()}{quality.ToString(CultureInfo.InvariantCulture)}{Rgb.Reset} {Palette.TextMuted.Fg()}(CRF {crf.ToString(CultureInfo.InvariantCulture)}){Rgb.Reset}",
                string.Empty,
            };

            foreach (var (preset, label) in QualityPresets)
            {
                var isCurrent = preset == quality;
                var mark = isCurrent ? $"{Palette.Emerald.Fg()}{pointer}{Rgb.Reset}" : " ";
                var textColor = isCurrent ? Palette.TextPrimary.Fg() : Palette.TextSecondary.Fg();
                rows.Add($"{mark} {Palette.Sky.Fg()}{preset.ToString(CultureInfo.InvariantCulture).PadLeft(2)}{Rgb.Reset}  {textColor}{label}{Rgb.Reset}");
            }

            var panel = PanelBuilder.Build("STEP 2/3 · SELECT QUALITY (1-100)", rows, Math.Min(width, 96), theme.Utf8Glyphs, Palette.BorderPrimary);
            DrawFullScreenMenu(stdout, headerLines, panel, width, height, theme.Utf8Glyphs, "↑/↓ preset  ·  ←/→ fine ±5  ·  Enter confirm  ·  q / Esc cancel");

            for (var poll = 0; poll < 2; poll++)
            {
                if (TerminalInput.KeyAvailableSafe())
                {
                    var ev = TerminalInput.ReadEvent();
                    if (ev is null)
                    {
                        return -1;
                    }

                    switch (ev.Value.Action)
                    {
                        case UiKeyAction.Up or UiKeyAction.WheelUp:
                            quality = NextPreset(quality, -1);
                            break;
                        case UiKeyAction.Down or UiKeyAction.WheelDown:
                            quality = NextPreset(quality, +1);
                            break;
                        case UiKeyAction.Left:
                            quality = Math.Max(1, quality - 5);
                            break;
                        case UiKeyAction.Right:
                            quality = Math.Min(100, quality + 5);
                            break;
                        case UiKeyAction.Enter:
                            return quality;
                        case UiKeyAction.Quit:
                            return -1;
                    }

                    break;
                }

                Thread.Sleep(11);
            }
        }
    }

    private static void DrawFullScreenMenu(
        TextWriter stdout,
        IReadOnlyList<string> headerLines,
        IReadOnlyList<string> panelLines,
        int width,
        int height,
        bool utf8,
        string hint)
    {
        var maxCols = Math.Max(10, width - 1);
        var sb = new StringBuilder(2048);
        sb.Append("\u001b[?2026h");
        var row = 1;

        for (var i = 0; i < headerLines.Count && row < height; i++, row++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H")
              .Append(AnsiText.ClipVisible(headerLines[i], maxCols))
              .Append("\u001b[0m\u001b[K");
        }

        if (row < height)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H\u001b[K");
            row++;
        }

        var panelWidth = panelLines.Count > 0 ? AnsiText.VisibleLength(panelLines[0]) : 0;
        var leftPad = panelWidth > 0 && maxCols > panelWidth ? new string(' ', (maxCols - panelWidth) / 2) : string.Empty;

        for (var i = 0; i < panelLines.Count && row < height; i++, row++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H")
              .Append(AnsiText.ClipVisible(leftPad + panelLines[i], maxCols))
              .Append("\u001b[0m\u001b[K");
        }

        while (row < height)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H\u001b[K");
            row++;
        }

        var diamond = utf8 ? "◆" : "*";
        var statusRaw = $"  {diamond} Drawplate  {hint}";
        var statusPadded = statusRaw.Length >= maxCols ? statusRaw[..maxCols] : statusRaw.PadRight(maxCols);
        sb.Append(CultureInfo.InvariantCulture, $"\u001b[{height};1H")
          .Append(Palette.StatusBg.Bg())
          .Append(Palette.StatusFg.Fg())
          .Append(statusPadded)
          .Append("\u001b[0m\u001b[?2026l");

        stdout.Write(sb.ToString());
        stdout.Flush();
    }

    private static int NextPreset(int current, int delta)
    {
        var idx = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < QualityPresets.Length; i++)
        {
            var dist = Math.Abs(QualityPresets[i].Quality - current);
            if (dist < bestDist)
            {
                bestDist = dist;
                idx = i;
            }
        }

        var next = Math.Clamp(idx + delta, 0, QualityPresets.Length - 1);
        return QualityPresets[next].Quality;
    }

    private static MenuResult Cancelled() => new()
    {
        Encoder = null,
        Quality = null,
        MaxSize = null,
        Error = Messages.N_INT(),
    };
}
