using System;
using System.Globalization;
using System.IO;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

internal sealed class PlainRenderer : IRenderer
{
    private readonly GlyphSet _glyphs;
    private readonly bool _utf8;

    public PlainRenderer(bool utf8)
    {
        _utf8 = utf8;
        _glyphs = utf8 ? Glyphs.Utf8 : Glyphs.Ascii;
    }

    public void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept)
    {
        global::System.Console.WriteLine("Plan");
        global::System.Console.WriteLine($"Input     {input}");
        global::System.Console.WriteLine($"Output    {output}");
        var crfText = crf.HasValue ? $" (CRF {crf.Value})" : "";
        global::System.Console.WriteLine($"Quality   {quality}{crfText}");
        global::System.Console.WriteLine($"Max size  {maxSize ?? "none"}");
        global::System.Console.WriteLine($"Encoder   {encoderChain ?? "[CPU]"}");
        global::System.Console.WriteLine($"Files     {fileCount} video(s) · {HumanFormat.Bytes(totalSize)} · timestamps {(timestampsKept ? "kept" : "not kept")}");
    }

    public void FileStarted(int index, int total, string name)
    {
        // Plain renderer doesn't show live progress
    }

    public void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed)
    {
        // Plain renderer doesn't show live progress
    }

    public void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason)
    {
        var glyph = success ? _glyphs.Ok : _glyphs.Fail;
        var arrow = _glyphs.Arrow;
        var encoderBadge = encoder ?? "[CPU]";
        var elapsedText = HumanFormat.Time(elapsed);

        if (success)
        {
            global::System.Console.WriteLine($"{glyph} [{index}/{total}] {name}  {HumanFormat.Bytes(inputSize)} {arrow} {HumanFormat.Bytes(outputSize)}  [{encoderBadge}]  {elapsedText}");
        }
        else
        {
            global::System.Console.WriteLine($"{glyph} [{index}/{total}] {name}  {reason}");
        }
    }

    public void Summary(int converted, int failed, int skipped, long inputSize, long outputSize, string encoderCounts, TimeSpan elapsed, string outputFolder)
    {
        global::System.Console.WriteLine("Summary");
        var resultParts = new System.Collections.Generic.List<string> { $"{converted} converted" };
        if (failed > 0) resultParts.Add($"{failed} failed");
        if (skipped > 0) resultParts.Add($"{skipped} skipped");
        global::System.Console.WriteLine($"Result    {string.Join("   ", resultParts)}");
        global::System.Console.WriteLine($"Size      {HumanFormat.Bytes(inputSize)} → {HumanFormat.Bytes(outputSize)}");
        global::System.Console.WriteLine($"Output    {outputFolder}");
    }

    public void Note(string text)
    {
        global::System.Console.WriteLine(text);
    }
}
