using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

/// <summary>Interactive Aurora-themed renderer with animated gradient banner, rounded panels, and colored summaries.</summary>
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
        var counter = $"{Palette.TextMuted.Fg()}[{Number(index)}/{Number(total)}]{Rgb.Reset}";
        if (!success)
        {
            var err = $"{Palette.Error.Fg()}{_glyphs.Fail}{Rgb.Reset} {counter} {Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  {Palette.Error.Fg()}{Sanitizer.ForDisplay(reason)}{Rgb.Reset}";
            _out.WriteLine(AnsiText.ClipVisible(err, Math.Max(30, _theme.Width - 2)));
            return;
        }

        var change = string.Empty;
        if (inputSize > 0)
        {
            var larger = outputSize > inputSize;
            var pct = larger ? Percent(outputSize - inputSize, inputSize) : Percent(inputSize - outputSize, inputSize);
            var deltaColor = larger ? Palette.Warn.Fg() : Palette.Emerald.Fg();
            var arrow = larger ? _glyphs.Up : _glyphs.Down;
            change = $"  {deltaColor}{arrow}{Number(pct)}%{Rgb.Reset}";
        }

        var encBadge = FormatEncoderBadge(encoder ?? "[CPU]");
        var line =
            $"{Palette.Emerald.Fg()}{_glyphs.Ok}{Rgb.Reset} {counter} " +
            $"{Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  " +
            $"{Palette.TextSecondary.Fg()}{HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {Palette.TextPrimary.Fg()}{HumanFormat.Bytes(outputSize)}{Rgb.Reset}" +
            $"{change}  {encBadge}  {Palette.TextMuted.Fg()}{HumanFormat.Time(elapsed)}{Rgb.Reset}";
        _out.WriteLine(AnsiText.ClipVisible(line, Math.Max(30, _theme.Width - 2)));
    }

    public void FileSkipped(int index, int total, string name, string reason)
    {
        var counter = $"{Palette.TextMuted.Fg()}[{Number(index)}/{Number(total)}]{Rgb.Reset}";
        var line = $"{Palette.TextMuted.Fg()}{_glyphs.Skip}{Rgb.Reset} {counter} {Palette.TextSecondary.Fg()}{Sanitizer.ForDisplay(name)}{Rgb.Reset}  {Palette.TextMuted.Fg()}skipped: {Sanitizer.ForDisplay(reason)}{Rgb.Reset}";
        _out.WriteLine(AnsiText.ClipVisible(line, Math.Max(30, _theme.Width - 2)));
    }

    public void Summary(int converted, int failed, int skipped, long inputSize, long outputSize, string encoderCounts, TimeSpan elapsed, string outputFolder)
    {
        _out.WriteLine();
        var statusLine = failed > 0
            ? $"{Palette.Warn.Fg()}Finished with {Number(failed)} failure(s).{Rgb.Reset}"
            : $"{Palette.Emerald.Fg()}All done.{Rgb.Reset}";

        var counts = new List<string>
        {
            $"{Palette.Emerald.Fg()}{Number(converted)} converted{Rgb.Reset}",
        };
        if (failed > 0)
        {
            counts.Add($"{Palette.Error.Fg()}{Number(failed)} failed{Rgb.Reset}");
        }

        if (skipped > 0)
        {
            counts.Add($"{Palette.TextSecondary.Fg()}{Number(skipped)} skipped{Rgb.Reset}");
        }

        var rows = new List<string>
        {
            PanelBuilder.KeyValue("Result", string.Join("   ", counts)),
        };

        if (converted > 0 && inputSize > 0)
        {
            var larger = outputSize > inputSize;
            var percent = larger ? Percent(outputSize - inputSize, inputSize) : Percent(inputSize - outputSize, inputSize);
            var deltaColor = larger ? Palette.Warn.Fg() : Palette.Emerald.Fg();
            var wording = larger
                ? $"{deltaColor}{_glyphs.Up} {Number(percent)}% larger{Rgb.Reset}"
                : $"{deltaColor}{_glyphs.Down} {Number(percent)}% smaller{Rgb.Reset}";
            rows.Add(PanelBuilder.KeyValue("Size",
                $"{Palette.TextSecondary.Fg()}{HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {Palette.TextPrimary.Fg()}{HumanFormat.Bytes(outputSize)}{Rgb.Reset}  {wording}"));

            var share = Percent(outputSize, inputSize);
            var ratio = Math.Clamp((double)outputSize / inputSize, 0.0, 1.0);
            var bar = Gradient.Bar(SummaryBarWidth, ratio, larger ? Palette.WarnRamp : Palette.SuccessRamp, frame: -1, utf8: _theme.Utf8Glyphs);
            rows.Add($"            {bar} {Palette.Cyan.Fg()}{Number(share)}% of original{Rgb.Reset}");

            if (!larger && inputSize - outputSize > 0)
            {
                rows.Add(PanelBuilder.KeyValue("Saved", $"{Palette.Emerald.Fg()}{HumanFormat.Bytes(inputSize - outputSize)}{Rgb.Reset}"));
            }
        }

        if (!string.IsNullOrEmpty(encoderCounts))
        {
            rows.Add(PanelBuilder.KeyValue("Encoders", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(encoderCounts)}{Rgb.Reset}"));
        }

        rows.Add(PanelBuilder.KeyValue("Time", $"{Palette.TextPrimary.Fg()}{HumanFormat.Time(elapsed)}{Rgb.Reset}"));
        rows.Add(PanelBuilder.KeyValue("Output", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(outputFolder)}{Rgb.Reset}"));
        rows.Add(PanelBuilder.KeyValue("Status", statusLine));

        var border = failed > 0 ? Palette.Warn : Palette.Emerald;
        var panel = PanelBuilder.Build("SUMMARY", rows, _theme.Width, _theme.Utf8Glyphs, border);
        foreach (var line in panel)
        {
            _out.WriteLine(line);
        }

        _out.Flush();
    }

    public void Note(string text)
    {
        var line = $"{Palette.Warn.Fg()}{Sanitizer.ForDisplay(text)}{Rgb.Reset}";
        _out.WriteLine(AnsiText.ClipVisible(line, Math.Max(30, _theme.Width - 2)));
    }

    private void RenderAnimatedBanner()
    {
        if (!_theme.CanRedraw || !_theme.IsInteractive)
        {
            foreach (var line in BrandLogo.RenderLines(_theme.Width, _theme.Utf8Glyphs))
            {
                _out.WriteLine(line);
            }

            _out.WriteLine();
            return;
        }

        var finalLines = BrandLogo.RenderLines(_theme.Width, _theme.Utf8Glyphs, frame: 8, reveal: 1.0);
        var height = finalLines.Count;
        try
        {
            _out.Write("\u001b[?25l");
            for (var step = 1; step <= 8; step++)
            {
                var reveal = step / 8.0;
                var frameLines = BrandLogo.RenderLines(_theme.Width, _theme.Utf8Glyphs, frame: step, reveal: reveal);
                var sb = new StringBuilder(512);
                sb.Append("\u001b[?2026h");
                if (step > 1)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\u001b[{height}A");
                }

                for (var r = 0; r < frameLines.Count; r++)
                {
                    sb.Append('\r').Append(frameLines[r]).Append("\u001b[K\n");
                }

                sb.Append("\u001b[?2026l");
                _out.Write(sb.ToString());
                _out.Flush();
                Thread.Sleep(28);
            }
        }
        finally
        {
            _out.Write("\u001b[?25h");
            _out.WriteLine();
            _out.Flush();
        }
    }

    private static string FormatEncoderChain(string chain)
    {
        var parts = chain.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = FormatEncoderBadge(parts[i]);
        }

        return string.Join($" {Palette.TextMuted.Fg()}>{Rgb.Reset} ", parts);
    }

    private static string FormatEncoderBadge(string badge) => badge.Equals("[CPU]", StringComparison.OrdinalIgnoreCase)
        ? $"{Palette.Sky.Fg()}{badge}{Rgb.Reset}"
        : $"{Palette.Emerald.Fg()}{badge}{Rgb.Reset}";

    private static long Percent(long part, long whole) =>
        (long)Math.Round(100.0 * part / whole, MidpointRounding.AwayFromZero);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
