using System;

namespace Drawplate.Console.Ui;

internal interface IRenderer
{
    void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept);
    void FileStarted(int index, int total, string name);
    void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed);
    void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason);
    void Summary(int converted, int failed, int skipped, long inputSize, long outputSize, string encoderCounts, TimeSpan elapsed, string outputFolder);
    void Note(string text);
}
