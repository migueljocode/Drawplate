using System.Text;

namespace Drawplate.Console.Cli;

/// <summary>Help text (R04). The Options block is generated from OptionTable; everything else is fixed.</summary>
internal static class HelpText
{
    private const string Preamble = """
drawplate - convert videos to H.264/AAC MP4 with FFmpeg

Usage:
  drawplate [options] [file ...]     convert the given files
  drawplate [options] -i <folder>    convert every video in a folder (not recursive)
  drawplate                          convert the current folder
  drawplate setup [-y] [-n]          show how to get FFmpeg; with consent, run the install command
  drawplate doctor                   read-only check of FFmpeg, encoders and hardware

Options:

""";

    private const string Epilogue = """

Exit codes: 0 ok, 1 failure, 2 usage error, 3 FFmpeg unavailable, 130 interrupted.

""";

    private const int LeftColumnWidth = 24;

    internal static string Render()
    {
        var builder = new StringBuilder(Preamble);
        foreach (var entry in OptionTable.Entries)
        {
            var left = entry.ShortName.Length > 0
                ? $"-{entry.ShortName}, {entry.LongNames[0]}"
                : $"    {entry.LongNames[0]}";
            if (entry.ValueName is { } value)
            {
                left += $" <{value}>";
            }

            builder.Append("  ").Append(left.PadRight(LeftColumnWidth)).Append("  ").Append(entry.Help).Append('\n');
        }

        return builder.Append(Epilogue).ToString();
    }
}
