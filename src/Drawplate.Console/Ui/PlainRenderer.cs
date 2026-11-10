using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

/// <summary>Plain text per D.4: no colors, no live lines. Writes to the given writer, never to the console directly.</summary>
internal sealed class PlainRenderer : IRenderer
{
    private const int BarWidth = 24;
    private const string Indent = "          ";

    private readonly TextWriter _out;
    private readonly GlyphSet _glyphs;

    public PlainRenderer(TextWriter output, bool utf8)
    {
        ArgumentNullException.ThrowIfNull(output);
        _out = output;
        _glyphs = utf8 ? Glyphs.Utf8 : Glyphs.Ascii;
    }

    public void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept)
    {
        var crfText = crf.HasValue ? $" (CRF {Number(crf.Value)})" : string.Empty;
        _out.WriteLine("Plan");
        _out.WriteLine($"Input     {Sanitizer.ForDisplay(input)}");
        _out.WriteLine($"Output    {Sanitizer.ForDisplay(output)}");
        _out.WriteLine($"Quality   {Number(quality)}{crfText}");
        _out.WriteLine($"Max size  {maxSize ?? "none"}");
        _out.WriteLine($"Encoder   {encoderChain ?? "[CPU]"}");
        _out.WriteLine($"Files     {Number(fileCount)} video(s) {_glyphs.Separator} {HumanFormat.Bytes(totalSize)} {_glyphs.Separator} timestamps {(timestampsKept ? "kept" : "not kept")}");
    }

    public void Unsupported(string name) =>
        _out.WriteLine($"{_glyphs.Skip} {Sanitizer.ForDisplay(name)} (unsupported file type)");

    public void FileStarted(int index, int total, string name)
    {
        // No live lines in plain mode.
    }

    public void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed)
    {
        // No live lines in plain mode.
    }

    public void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason)
    {
        var shown = Sanitizer.ForDisplay(name);
        if (!success)
        {
            _out.WriteLine($"{_glyphs.Fail} [{Number(index)}/{Number(total)}] {shown}  {Sanitizer.ForDisplay(reason)}");
            return;
        }

        var change = inputSize > 0 ? $"  {Change(inputSize, outputSize)}" : string.Empty;
        _out.WriteLine($"{_glyphs.Ok} [{Number(index)}/{Number(total)}] {shown}  {HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {HumanFormat.Bytes(outputSize)}{change}  {encoder ?? "[CPU]"}  {HumanFormat.Time(elapsed)}");
    }

    public void FileSkipped(int index, int total, string name, string reason) =>
        _out.WriteLine($"{_glyphs.Skip} [{Number(index)}/{Number(total)}] {Sanitizer.ForDisplay(name)}  skipped: {Sanitizer.ForDisplay(reason)}");

    public void Summary(int converted, int failed, int skipped, long inputSize, long outputSize, string encoderCounts, TimeSpan elapsed, string outputFolder)
    {
        var result = new List<string> { $"{Number(converted)} converted" };
        if (failed > 0)
        {
            result.Add($"{Number(failed)} failed");
        }

        if (skipped > 0)
        {
            result.Add($"{Number(skipped)} skipped");
        }

        _out.WriteLine("Summary");
        _out.WriteLine($"Result    {string.Join("   ", result)}");

        if (converted > 0 && inputSize > 0)
        {
            var larger = outputSize > inputSize;
            var percent = larger ? Percent(outputSize - inputSize, inputSize) : Percent(inputSize - outputSize, inputSize);
            var wording = larger ? $"{_glyphs.Up} {Number(percent)}% larger" : $"{_glyphs.Down} {Number(percent)}% smaller";
            _out.WriteLine($"Size      {HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {HumanFormat.Bytes(outputSize)}  {wording}");

            var share = Percent(outputSize, inputSize);
            var filled = Math.Clamp((int)Math.Round(BarWidth * (double)outputSize / inputSize, MidpointRounding.AwayFromZero), 0, BarWidth);
            _out.WriteLine($"{Indent}{Repeat(_glyphs.BarFull, filled)}{Repeat(_glyphs.BarEmpty, BarWidth - filled)} {Number(share)}% of original");

            if (!larger && inputSize - outputSize > 0)
            {
                _out.WriteLine($"Saved     {HumanFormat.Bytes(inputSize - outputSize)}");
            }
        }

        if (!string.IsNullOrEmpty(encoderCounts))
        {
            _out.WriteLine($"Encoders  {encoderCounts}");
        }

        _out.WriteLine($"Time      {HumanFormat.Time(elapsed)}");
        _out.WriteLine($"Output    {Sanitizer.ForDisplay(outputFolder)}");
        _out.WriteLine(failed > 0 ? $"Finished with {Number(failed)} failure(s)." : "All done.");
    }

    public void Note(string text) => _out.WriteLine(text);

    /// <summary>The per-file size change such as <c>↓32%</c>, or <c>↑5%</c> when the output is larger.</summary>
    private string Change(long inputSize, long outputSize) => outputSize > inputSize
        ? $"{_glyphs.Up}{Number(Percent(outputSize - inputSize, inputSize))}%"
        : $"{_glyphs.Down}{Number(Percent(inputSize - outputSize, inputSize))}%";

    private static long Percent(long part, long whole) =>
        (long)Math.Round(100.0 * part / whole, MidpointRounding.AwayFromZero);

    private static string Repeat(string glyph, int count) =>
        count <= 0 ? string.Empty : string.Concat(System.Linq.Enumerable.Repeat(glyph, count));

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
