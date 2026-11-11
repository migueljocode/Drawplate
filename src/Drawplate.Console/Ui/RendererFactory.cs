using System;
using System.IO;

namespace Drawplate.Console.Ui;

internal static class RendererFactory
{
    public static IRenderer Create(TextWriter output, Theme theme, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(theme);
        var plain = new PlainRenderer(output, theme.Utf8Glyphs);
        return theme.CanRedraw
            ? new TerminalRenderer(plain, new LiveProgress(output, theme, timeProvider ?? TimeProvider.System))
            : plain;
    }

    private sealed class TerminalRenderer(IRenderer inner, LiveProgress progress) : IRenderer
    {
        private string? _activeName;

        public void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept) =>
            inner.PlanCard(input, output, quality, crf, maxSize, encoderChain, fileCount, totalSize, timestampsKept);

        public void Unsupported(string name) => inner.Unsupported(name);

        public void FileStarted(int index, int total, string name)
        {
            _activeName = name;
            progress.Draw(index, total, name, null, null, TimeSpan.Zero);
            inner.FileStarted(index, total, name);
        }

        public void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed) =>
            progress.Draw(index, total, _activeName ?? name, fraction, speed, elapsed);

        public void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason)
        {
            progress.Clear();
            _activeName = null;
            inner.FileFinished(index, total, name, success, encoder, elapsed, inputSize, outputSize, reason);
        }

        public void FileSkipped(int index, int total, string name, string reason)
        {
            progress.Clear();
            inner.FileSkipped(index, total, name, reason);
        }

        public void Summary(int converted, int failed, int skipped, long inputSize, long outputSize, string encoderCounts, TimeSpan elapsed, string outputFolder)
        {
            progress.Clear();
            inner.Summary(converted, failed, skipped, inputSize, outputSize, encoderCounts, elapsed, outputFolder);
        }

        public void Note(string text) => inner.Note(text);
    }
}
