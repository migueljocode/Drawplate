using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Drawplate.Models.Formatting;
using Drawplate.Models.Setup;
using Drawplate.Services.Setup;

namespace Drawplate.Console.Ui;

/// <summary>Aurora-themed panels for <c>drawplate doctor</c> and <c>drawplate setup</c> on interactive terminals.</summary>
internal static class RichStatusView
{
    public static void WriteDoctor(TextWriter stdout, DoctorReport report, PlatformInfo platform, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(theme);

        foreach (var line in BrandLogo.RenderLines(theme.Width, theme.Utf8Glyphs, frame: 0, reveal: 1.0, subtitle: "Environment & Hardware Diagnostics"))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine();
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

    public static void WriteSetupBanner(TextWriter stdout, Theme theme, PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(platform);

        foreach (var line in BrandLogo.RenderLines(theme.Width, theme.Utf8Glyphs, frame: 0, reveal: 1.0, subtitle: $"FFmpeg Setup  ({platform.Os})"))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine();
        stdout.Flush();
    }
}
