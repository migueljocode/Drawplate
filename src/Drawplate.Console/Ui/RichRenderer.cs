using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

/// <summary>Interactive Aurora-themed renderer with top-margined gradient banner, rounded panels, and colored summaries.</summary>
internal sealed class RichRenderer : IRenderer
{
    private const int SummaryBarWidth = 28;
    private readonly TextWriter _out;
    private readonly Theme _theme;
    private readonly GlyphSet _glyphs;

    public RichRenderer(TextWriter output, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(theme);
        _out = output;
        _theme = theme;
        _glyphs = theme.Utf8Glyphs ? Glyphs.Utf8 : Glyphs.Ascii;
    }

    public void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept)
    {
        RenderAnimatedBanner();

        var crfText = crf.HasValue
            ? $" {Palette.TextMuted.Fg()}(CRF {Number(crf.Value)}){Rgb.Reset}"
            : string.Empty;
        var qualityText = $"{Palette.Cyan.Fg()}{Number(quality)}{Rgb.Reset}{crfText}";
        var maxSizeText = maxSize is null
            ? $"{Palette.TextMuted.Fg()}none{Rgb.Reset}"
            : $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(maxSize)}{Rgb.Reset}";
        var encoderText = FormatEncoderChain(encoderChain ?? "[CPU]");
        var tsText = timestampsKept
            ? $"{Palette.Emerald.Fg()}timestamps kept{Rgb.Reset}"
            : $"{Palette.Warn.Fg()}timestamps not kept{Rgb.Reset}";
        var filesSummary =
            $"{Palette.TextPrimary.Fg()}{Number(fileCount)} video(s){Rgb.Reset} " +
            $"{Palette.TextMuted.Fg()}{_glyphs.Separator}{Rgb.Reset} " +
            $"{Palette.Cyan.Fg()}{HumanFormat.Bytes(totalSize)}{Rgb.Reset} " +
            $"{Palette.TextMuted.Fg()}{_glyphs.Separator}{Rgb.Reset} {tsText}";

        var rows = new List<string>
        {
            PanelBuilder.KeyValue("Input", $"{Palette.TextPrimary.Fg()}{Sanitizer.ForDisplay(input)}{Rgb.Reset}"),
            PanelBuilder.KeyValue("Output", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(output)}{Rgb.Reset}"),
            PanelBuilder.KeyValue("Quality", qualityText),
            PanelBuilder.KeyValue("Max size", maxSizeText),
            PanelBuilder.KeyValue("Encoder", encoderText),
            PanelBuilder.KeyValue("Files", filesSummary),
        };

        var panel = PanelBuilder.Build("CONVERSION PLAN", rows, _theme.Width, _theme.Utf8Glyphs, Palette.BorderPrimary);
        foreach (var line in panel)
        {
            _out.WriteLine(line);
        }

        _out.WriteLine();
        _out.Flush();
    }

    public void Unsupported(string name)
    {
        var shown = Sanitizer.ForDisplay(name);
        _out.WriteLine(AnsiText.ClipVisible(
            $"{Palette.TextMuted.Fg()}{_glyphs.Skip} {shown} (unsupported file type){Rgb.Reset}",
            Math.Max(30, _theme.Width - 2)));
    }

    public void FileStarted(int index, int total, string name)
    {
        // LiveProgress handles redrawing while encoding.
    }

    public void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed)
    {
        // LiveProgress handles redrawing while encoding.
    }

    public void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason)
    {
        var shown = Sanitizer.ForDisplay(name);
        var counter = $"[{Number(index)}/{Number(total)}]";
        string line;

        if (success)
        {
            var delta = FormatSizeDelta(inputSize, outputSize);
            var badge = FormatEncoderBadge(encoder ?? "[CPU]");
            line =
                $"{Palette.Emerald.Fg()}{_glyphs.Ok}{Rgb.Reset} " +
                $"{Palette.Sky.Fg()}{counter}{Rgb.Reset} " +
                $"{Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  " +
                $"{Palette.TextSecondary.Fg()}{HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {Palette.TextPrimary.Fg()}{HumanFormat.Bytes(outputSize)}{Rgb.Reset}  " +
                $"{delta}  {badge}  {Palette.TextMuted.Fg()}{HumanFormat.Time(elapsed)}{Rgb.Reset}";
        }
        else
        {
            var why = Sanitizer.ForDisplay(reason ?? "failed");
            line =
                $"{Palette.Error.Fg()}{_glyphs.Fail}{Rgb.Reset} " +
                $"{Palette.Sky.Fg()}{counter}{Rgb.Reset} " +
                $"{Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  " +
                $"{Palette.Error.Fg()}failed: {why}{Rgb.Reset}";
        }

        _out.WriteLine(AnsiText.ClipVisible(line, Math.Max(40, _theme.Width - 2)));
        _out.Flush();
    }

    public void FileSkipped(int index, int total, string name, string reason)
    {
        var shown = Sanitizer.ForDisplay(name);
        var why = Sanitizer.ForDisplay(reason);
        var counter = $"[{Number(index)}/{Number(total)}]";
        var line =
            $"{Palette.TextMuted.Fg()}{_glyphs.Skip}{Rgb.Reset} " +
            $"{Palette.TextSecondary.Fg()}{counter}{Rgb.Reset} " +
            $"{Palette.TextSecondary.Fg()}{shown}{Rgb.Reset}  " +
            $"{Palette.TextMuted.Fg()}skipped: {why}{Rgb.Reset}";
        _out.WriteLine(AnsiText.ClipVisible(line, Math.Max(40, _theme.Width - 2)));
        _out.Flush();
    }

    public void Note(string text)
    {
        var shown = Sanitizer.ForDisplay(text);
        _out.WriteLine(AnsiText.ClipVisible(
            $"{Palette.Warn.Fg()}!{Rgb.Reset} {Palette.TextSecondary.Fg()}{shown}{Rgb.Reset}",
            Math.Max(40, _theme.Width - 2)));
        _out.Flush();
    }

    public void Summary(int converted, int failed, int skipped, long inputBytes, long outputBytes, string encoderCounts, TimeSpan elapsed, string outputFolder)
    {
        var resultParts = new List<string>
        {
            $"{Palette.Emerald.Fg()}{Number(converted)} converted{Rgb.Reset}",
        };
        if (failed > 0)
        {
            resultParts.Add($"{Palette.Error.Fg()}{Number(failed)} failed{Rgb.Reset}");
        }

        if (skipped > 0)
        {
            resultParts.Add($"{Palette.TextSecondary.Fg()}{Number(skipped)} skipped{Rgb.Reset}");
        }

        var rows = new List<string>
        {
            PanelBuilder.KeyValue("Result", string.Join("   ", resultParts)),
        };

        if (converted > 0 && inputBytes > 0)
        {
            var diff = inputBytes - outputBytes;
            var pct = Math.Clamp((double)outputBytes / inputBytes, 0.0, 1.0);
            var deltaPct = (int)Math.Round(Math.Abs(diff) * 100.0 / inputBytes, MidpointRounding.AwayFromZero);
            var arrowLabel = diff >= 0
                ? $"{Palette.Emerald.Fg()}{_glyphs.Down} {Number(deltaPct)}% smaller{Rgb.Reset}"
                : $"{Palette.Warn.Fg()}{_glyphs.Up} {Number(deltaPct)}% larger{Rgb.Reset}";
            rows.Add(PanelBuilder.KeyValue(
                "Size",
                $"{Palette.TextSecondary.Fg()}{HumanFormat.Bytes(inputBytes)} {_glyphs.Arrow} {Palette.TextPrimary.Fg()}{HumanFormat.Bytes(outputBytes)}{Rgb.Reset}  {arrowLabel}"));

            var ramp = diff >= 0 ? Palette.SuccessRamp : Palette.WarnRamp;
            var bar = Gradient.BarAt(Math.Min(SummaryBarWidth, Math.Max(12, _theme.Width - 36)), pct, ramp, timeSeconds: -1.0, utf8: _theme.Utf8Glyphs);
            var ratioPct = (int)Math.Round(pct * 100.0, MidpointRounding.AwayFromZero);
            rows.Add(PanelBuilder.KeyValue(string.Empty, $"{bar} {Palette.TextSecondary.Fg()}{Number(ratioPct)}% of original{Rgb.Reset}"));

            var savedLabel = diff >= 0 ? "Saved" : "Added";
            var savedColor = diff >= 0 ? Palette.Emerald : Palette.Warn;
            rows.Add(PanelBuilder.KeyValue(savedLabel, $"{savedColor.Fg()}{HumanFormat.Bytes(Math.Abs(diff))}{Rgb.Reset}"));
        }

        if (!string.IsNullOrEmpty(encoderCounts))
        {
            rows.Add(PanelBuilder.KeyValue("Encoders", FormatEncoderChain(encoderCounts)));
        }

        rows.Add(PanelBuilder.KeyValue("Time", $"{Palette.Cyan.Fg()}{HumanFormat.Time(elapsed)}{Rgb.Reset}"));
        rows.Add(PanelBuilder.KeyValue("Output", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(outputFolder)}{Rgb.Reset}"));

        var statusColor = failed == 0 ? Palette.Emerald : Palette.Error;
        var statusText = failed == 0 ? "All done." : $"{Number(failed)} file(s) failed.";
        rows.Add(PanelBuilder.KeyValue("Status", $"{statusColor.Fg()}{statusText}{Rgb.Reset}"));

        _out.WriteLine();
        var border = failed == 0 ? Palette.Emerald : Palette.Error;
        foreach (var line in PanelBuilder.Build("SUMMARY", rows, _theme.Width, _theme.Utf8Glyphs, border))
        {
            _out.WriteLine(line);
        }

        _out.Flush();
    }

    private void RenderAnimatedBanner()
    {
        if (!_theme.CanRedraw || !_theme.IsInteractive)
        {
            foreach (var line in BrandLogo.RenderLinesAt(_theme.Width, _theme.Utf8Glyphs, timeSeconds: 0.2, reveal: 1.0, topMargin: BrandLogo.TopMargin))
            {
                _out.WriteLine(line);
            }

            _out.WriteLine();
            return;
        }

        var final = BrandLogo.RenderLinesAt(_theme.Width, _theme.Utf8Glyphs, timeSeconds: 0.3, reveal: 1.0, topMargin: BrandLogo.TopMargin);
        var height = final.Count;
        var maxCols = Math.Max(20, _theme.Width - 1);
        var clock = Stopwatch.StartNew();

        _out.Write("\u001b[?25l");
        try
        {
            for (var step = 1; step <= 8; step++)
            {
                var frameLines = BrandLogo.RenderLinesAt(_theme.Width, _theme.Utf8Glyphs, clock.Elapsed.TotalSeconds, reveal: step / 8.0, topMargin: BrandLogo.TopMargin);
                var sb = new StringBuilder(512);
                sb.Append("\u001b[?2026h");
                if (step > 1)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\u001b[{height}A");
                }

                foreach (var line in frameLines)
                {
                    sb.Append('\r').Append(AnsiText.ClipVisible(line, maxCols)).Append("\u001b[K\n");
                }

                sb.Append("\u001b[?2026l");
                _out.Write(sb.ToString());
                _out.Flush();
                Thread.Sleep(16);
            }
        }
        finally
        {
            _out.Write("\u001b[?25h");
        }

        _out.WriteLine();
        _out.Flush();
    }

    internal static string FormatEncoderChain(string chain)
    {
        var parts = chain.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i] == ">"
                ? $"{Palette.TextMuted.Fg()}>{Rgb.Reset}"
                : FormatEncoderBadge(parts[i]);
        }

        return string.Join(' ', parts);
    }

    internal static string FormatEncoderBadge(string badge)
    {
        if (badge.StartsWith("[CPU", StringComparison.OrdinalIgnoreCase))
        {
            return $"{Palette.Sky.Fg()}{badge}{Rgb.Reset}";
        }

        if (badge.StartsWith('['))
        {
            return $"{Palette.Emerald.Fg()}{badge}{Rgb.Reset}";
        }

        return $"{Palette.TextSecondary.Fg()}{badge}{Rgb.Reset}";
    }

    internal string FormatSizeDelta(long inputBytes, long outputBytes)
    {
        if (inputBytes <= 0)
        {
            return string.Empty;
        }

        var diff = inputBytes - outputBytes;
        var pct = (int)Math.Round(Math.Abs(diff) * 100.0 / inputBytes, MidpointRounding.AwayFromZero);
        return diff >= 0
            ? $"{Palette.Emerald.Fg()}{_glyphs.Down}{Number(pct)}%{Rgb.Reset}"
            : $"{Palette.Warn.Fg()}{_glyphs.Up}{Number(pct)}%{Rgb.Reset}";
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
