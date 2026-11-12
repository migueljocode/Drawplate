using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Formatting;
using Drawplate.Models.Setup;
using Drawplate.Services.Setup;

namespace Drawplate.Console.Ui;

/// <summary>Aurora-themed panels and instant async probe spinner for doctor, encoder listing (-l), and setup.</summary>
internal static class RichStatusView
{
    public static T RunWithSpinner<T>(TextWriter stdout, Theme theme, string label, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(work);

        var task = Task.Run(() => work(ct), ct);
        if (!theme.Colors || !theme.CanRedraw || !theme.IsInteractive)
        {
            return task.GetAwaiter().GetResult();
        }

        var clock = Stopwatch.StartNew();
        var maxCols = Math.Max(20, theme.Width - 1);
        var drawn = false;
        var shownAt = 0.0;

        stdout.Write("\u001b[?25l");
        try
        {
            while (!task.IsCompleted)
            {
                var t = clock.Elapsed.TotalSeconds;
                if (drawn || t >= 0.035)
                {
                    if (!drawn)
                    {
                        shownAt = t;
                        drawn = true;
                    }

                    var spin = Gradient.SpinnerAt(t);
                    var spinColor = Gradient.Wave(Palette.AuroraBright, t * 0.4, 0.0);
                    var scanner = Gradient.IndeterminateBarAt(18, t, theme.Utf8Glyphs);
                    var line = AnsiText.ClipVisible(
                        $"{spinColor.Fg()}{spin}{Rgb.Reset} {Palette.TextPrimary.Fg()}{label}{Rgb.Reset}  {scanner}",
                        maxCols);
                    stdout.Write($"\r{line}\u001b[K");
                    stdout.Flush();
                }

                Thread.Sleep(22);
            }

            if (drawn && clock.Elapsed.TotalSeconds - shownAt < 0.08)
            {
                Thread.Sleep(25);
            }
        }
        finally
        {
            if (drawn)
            {
                stdout.Write("\r\u001b[2K\u001b[0m");
            }

            stdout.Write("\u001b[?25h");
            stdout.Flush();
        }

        return task.GetAwaiter().GetResult();
    }

    public static void WriteDoctorBanner(TextWriter stdout, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        foreach (var line in BrandLogo.RenderLinesAt(theme.Width, theme.Utf8Glyphs, timeSeconds: 0.25, reveal: 1.0, subtitle: "Environment & Hardware Diagnostics", topMargin: BrandLogo.TopMargin))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine();
        stdout.Flush();
    }

    public static void WriteDoctor(TextWriter stdout, DoctorReport report, PlatformInfo platform, Theme theme, bool includeBanner = true)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(theme);

        if (includeBanner)
        {
            WriteDoctorBanner(stdout, theme);
        }

        var glyphs = theme.Utf8Glyphs ? Glyphs.Utf8 : Glyphs.Ascii;
        var rows = new List<string>(report.Lines.Length + 2);
        foreach (var item in report.Lines)
        {
            var badge = item.Status switch
            {
                "ok" => $"{Palette.Emerald.Fg()}{glyphs.Ok} ok      {Rgb.Reset}",
                "note" => $"{Palette.Sky.Fg()}i note    {Rgb.Reset}",
                "outdated" => $"{Palette.Warn.Fg()}! old     {Rgb.Reset}",
                "missing" => $"{Palette.TextMuted.Fg()}o missing {Rgb.Reset}",
                _ => $"{Palette.Error.Fg()}{glyphs.Fail} {item.Status.PadRight(7)}{Rgb.Reset}",
            };

            var check = $"{Palette.Cyan.Fg()}{item.Check.PadRight(10)}{Rgb.Reset}";
            var detail = $"{Palette.TextPrimary.Fg()}{Sanitizer.ForDisplay(item.Detail)}{Rgb.Reset}";
            rows.Add($"{badge} {check}  {detail}");
        }

        var border = report.ExitCode == 0 ? Palette.Emerald : Palette.Warn;
        foreach (var line in PanelBuilder.Build("DRAWPLATE DOCTOR", rows, theme.Width, theme.Utf8Glyphs, border))
        {
            stdout.WriteLine(line);
        }

        if (report.Error is { } error)
        {
            stdout.WriteLine();
            var errRows = new List<string> { $"{Palette.Error.Fg()}{Sanitizer.ForDisplay(error.Text)}{Rgb.Reset}" };
            if (error.Hint is { } hint)
            {
                errRows.Add($"{Palette.TextSecondary.Fg()}{Sanitizer.ForDisplay(hint)}{Rgb.Reset}");
            }

            foreach (var line in PanelBuilder.Build("ACTION REQUIRED", errRows, theme.Width, theme.Utf8Glyphs, Palette.Error))
            {
                stdout.WriteLine(line);
            }
        }
        else if (report.Lines.Any(line => line.Check == "FFmpeg" && line.Status == "missing"))
        {
            stdout.WriteLine();
            var guidance = InstallAdvisor.NotFoundText(platform, InstallAdvisor.Suggestions(platform))
                .Split('\n')
                .Select(l => $"{Palette.TextSecondary.Fg()}{l}{Rgb.Reset}")
                .ToList();
            foreach (var line in PanelBuilder.Build("INSTALL FFMPEG", guidance, theme.Width, theme.Utf8Glyphs, Palette.BorderPrimary))
            {
                stdout.WriteLine(line);
            }
        }

        stdout.Flush();
    }

    public static void WriteEncoders(TextWriter stdout, ImmutableArray<EncoderKind> chain, string? note, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        foreach (var line in BrandLogo.RenderLinesAt(theme.Width, theme.Utf8Glyphs, timeSeconds: 0.25, reveal: 1.0, subtitle: "Available Hardware & CPU Encoders", topMargin: BrandLogo.TopMargin))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine();
        var glyphs = theme.Utf8Glyphs ? Glyphs.Utf8 : Glyphs.Ascii;
        var rows = new List<string>(chain.Length + 2);
        for (var i = 0; i < chain.Length; i++)
        {
            var kind = chain[i];
            var badge = RichRenderer.FormatEncoderBadge(EncoderSets.Badge(kind));
            var role = i == 0 ? "primary" : "fallback";
            rows.Add($"{Palette.Emerald.Fg()}{glyphs.Ok}{Rgb.Reset}  {badge}  {Palette.Cyan.Fg()}{EncoderSets.Id(kind).PadRight(14)}{Rgb.Reset}  {Palette.TextMuted.Fg()}{role}{Rgb.Reset}");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            rows.Add(string.Empty);
            rows.Add($"{Palette.Warn.Fg()}!{Rgb.Reset} {Palette.TextSecondary.Fg()}{Sanitizer.ForDisplay(note)}{Rgb.Reset}");
        }

        foreach (var line in PanelBuilder.Build("AVAILABLE ENCODERS", rows, theme.Width, theme.Utf8Glyphs, Palette.BorderPrimary))
        {
            stdout.WriteLine(line);
        }

        stdout.Flush();
    }

    public static void WriteSetupBanner(TextWriter stdout, Theme theme, PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(platform);

        foreach (var line in BrandLogo.RenderLinesAt(theme.Width, theme.Utf8Glyphs, timeSeconds: 0.25, reveal: 1.0, subtitle: $"FFmpeg Setup  ({platform.Os})", topMargin: BrandLogo.TopMargin))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine();
        stdout.Flush();
    }
}
