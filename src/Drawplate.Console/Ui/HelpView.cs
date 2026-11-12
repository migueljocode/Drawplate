using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Drawplate.Console.Cli;

namespace Drawplate.Console.Ui;

/// <summary>Renders a responsive, Aurora-themed help experience and feeds HelpPager.</summary>
internal static class HelpView
{
    private static readonly (string Title, string[] LongNames)[] CategoryGroups =
    [
        ("INPUT & OUTPUT", ["--input", "--output", "--no-timestamps"]),
        ("QUALITY & ENCODING", ["--quality", "--max-size", "--encoder", "--cpu", "--list-encoders"]),
        ("SETUP & DIAGNOSTICS", ["--interactive", "--install-deps", "--no-install", "--ffmpeg-path", "--verbose"]),
        ("GENERAL", ["--help", "--version"]),
    ];

    public static void Show(TextWriter stdout, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        HelpPager.Show(stdout, theme);
    }

    /// <summary>Builds the scrollable body lines (everything below the animated header) for a given terminal width.</summary>
    internal static IReadOnlyList<string> BuildBodyLines(int width, bool utf8, string? searchQuery = null)
    {
        var lines = new List<string>();
        var maxPanelWidth = Math.Max(40, width);

        AddSection(lines, "USAGE", BuildUsageLines(maxPanelWidth, utf8), maxPanelWidth, utf8, Palette.BorderPrimary, searchQuery);

        var entryByLong = OptionTable.Entries.ToDictionary(e => e.LongNames[0], StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (categoryTitle, names) in CategoryGroups)
        {
            var groupEntries = new List<OptionEntry>();
            foreach (var name in names)
            {
                if (entryByLong.TryGetValue(name, out var entry))
                {
                    groupEntries.Add(entry);
                    used.Add(name);
                }
            }

            if (groupEntries.Count > 0)
            {
                AddSection(lines, categoryTitle, BuildOptionLines(groupEntries, maxPanelWidth), maxPanelWidth, utf8, Palette.BorderSecondary, searchQuery);
            }
        }

        var remaining = OptionTable.Entries.Where(e => !used.Contains(e.LongNames[0])).ToList();
        if (remaining.Count > 0)
        {
            AddSection(lines, "OTHER OPTIONS", BuildOptionLines(remaining, maxPanelWidth), maxPanelWidth, utf8, Palette.BorderSecondary, searchQuery);
        }

        AddSection(lines, "EXAMPLES", BuildExampleLines(maxPanelWidth, utf8), maxPanelWidth, utf8, Palette.BorderPrimary, searchQuery);
        AddSection(lines, "EXIT CODES", BuildExitCodeLines(maxPanelWidth), maxPanelWidth, utf8, Palette.BorderSubtle, searchQuery);

        return lines;
    }

    private static void AddSection(
        List<string> target,
        string title,
        IReadOnlyList<string> body,
        int width,
        bool utf8,
        Rgb borderColor,
        string? searchQuery)
    {
        var highlighted = string.IsNullOrEmpty(searchQuery)
            ? body
            : body.Select(line => HighlightSearch(line, searchQuery)).ToList();
        var panel = PanelBuilder.Build(title, highlighted, width, utf8, borderColor);
        target.AddRange(panel);
    }

    private static List<string> BuildUsageLines(int width, bool utf8)
    {
        var dot = utf8 ? "·" : "-";
        var commands = new (string Cmd, string Desc)[]
        {
            ("drawplate [options] [file ...]", "Convert the given video files"),
            ("drawplate [options] -i <folder>", "Convert every video in a folder (not recursive)"),
            ("drawplate", "Convert all videos in the current folder"),
            ("drawplate setup [-y] [-n]", "Check FFmpeg and optionally run the installer"),
            ("drawplate doctor", "Read-only diagnostics for FFmpeg, encoders & GPU"),
        };

        var inner = Math.Max(24, width - 6);
        var cmdWidth = Math.Min(34, Math.Max(22, inner / 2));
        var lines = new List<string>(commands.Length * 2);
        foreach (var (cmd, desc) in commands)
        {
            if (inner < 62)
            {
                lines.Add($"{Palette.Cyan.Fg()}{cmd}{Rgb.Reset}");
                var wrapped = AnsiText.WrapPlain(desc, Math.Max(14, inner - 4));
                for (var i = 0; i < wrapped.Count; i++)
                {
                    var prefix = i == 0 ? $"  {dot} " : "    ";
                    lines.Add($"{Palette.TextSecondary.Fg()}{prefix}{wrapped[i]}{Rgb.Reset}");
                }
            }
            else
            {
                var descWidth = Math.Max(16, inner - cmdWidth - 2);
                var wrapped = AnsiText.WrapPlain(desc, descWidth);
                lines.Add($"{Palette.Cyan.Fg()}{cmd.PadRight(cmdWidth)}{Rgb.Reset}  {Palette.TextSecondary.Fg()}{wrapped[0]}{Rgb.Reset}");
                for (var i = 1; i < wrapped.Count; i++)
                {
                    lines.Add($"{new string(' ', cmdWidth + 2)}{Palette.TextSecondary.Fg()}{wrapped[i]}{Rgb.Reset}");
                }
            }
        }

        return lines;
    }

    private static List<string> BuildOptionLines(List<OptionEntry> entries, int width)
    {
        var inner = Math.Max(24, width - 6);
        var keyWidth = Math.Min(28, Math.Max(20, inner / 3));
        var lines = new List<string>(entries.Count * 2);

        foreach (var entry in entries)
        {
            var left = FormatKey(entry);
            if (inner < 58)
            {
                lines.Add($"{Palette.Emerald.Fg()}{left}{Rgb.Reset}");
                foreach (var wrapped in AnsiText.WrapPlain(entry.Help, inner - 2))
                {
                    lines.Add($"  {Palette.TextSecondary.Fg()}{wrapped}{Rgb.Reset}");
                }
            }
            else
            {
                var descWidth = Math.Max(16, inner - keyWidth - 2);
                var wrapped = AnsiText.WrapPlain(entry.Help, descWidth);
                lines.Add($"{Palette.Emerald.Fg()}{left.PadRight(keyWidth)}{Rgb.Reset}  {Palette.TextPrimary.Fg()}{wrapped[0]}{Rgb.Reset}");
                for (var i = 1; i < wrapped.Count; i++)
                {
                    lines.Add($"{new string(' ', keyWidth + 2)}{Palette.TextSecondary.Fg()}{wrapped[i]}{Rgb.Reset}");
                }
            }
        }

        return lines;
    }

    private static List<string> BuildExampleLines(int width, bool utf8)
    {
        var arrow = utf8 ? "▸" : ">";
        var inner = Math.Max(24, width - 6);
        var examples = new (string Cmd, string Comment)[]
        {
            ("drawplate", "# convert current folder"),
            ("drawplate -i ~/Videos -q 75 -s hd", "# 720p cap at quality 75"),
            ("drawplate clip1.mov clip2.avi -c -q 85", "# explicit files on CPU"),
            ("drawplate -m", "# interactive wizard"),
            ("drawplate doctor", "# inspect FFmpeg & GPU"),
        };

        var lines = new List<string>(examples.Length * 2);
        foreach (var (cmd, comment) in examples)
        {
            if (inner < 68)
            {
                lines.Add($"{Palette.Sky.Fg()}{arrow}{Rgb.Reset} {Palette.TextPrimary.Fg()}{cmd}{Rgb.Reset}");
                lines.Add($"  {Palette.TextMuted.Fg()}{comment}{Rgb.Reset}");
            }
            else
            {
                var cmdCol = Math.Min(42, Math.Max(30, inner - 26));
                lines.Add($"{Palette.Sky.Fg()}{arrow}{Rgb.Reset} {Palette.TextPrimary.Fg()}{cmd.PadRight(cmdCol)}{Rgb.Reset} {Palette.TextMuted.Fg()}{comment}{Rgb.Reset}");
            }
        }

        return lines;
    }

    private static List<string> BuildExitCodeLines(int width)
    {
        var inner = Math.Max(24, width - 6);
        var line1 =
            $"{Palette.Emerald.Fg()}0{Rgb.Reset} {Palette.TextSecondary.Fg()}ok{Rgb.Reset}   " +
            $"{Palette.Error.Fg()}1{Rgb.Reset} {Palette.TextSecondary.Fg()}failure{Rgb.Reset}   " +
            $"{Palette.Warn.Fg()}2{Rgb.Reset} {Palette.TextSecondary.Fg()}usage error{Rgb.Reset}";
        var line2 =
            $"{Palette.Cyan.Fg()}3{Rgb.Reset} {Palette.TextSecondary.Fg()}FFmpeg unavailable{Rgb.Reset}   " +
            $"{Palette.Sky.Fg()}130{Rgb.Reset} {Palette.TextSecondary.Fg()}interrupted{Rgb.Reset}";
        return inner < 66 ? [line1, line2] : [$"{line1}   {line2}"];
    }

    private static string FormatKey(OptionEntry entry)
    {
        var shortPart = entry.ShortName.Length > 0 ? $"-{entry.ShortName}, " : "    ";
        var value = entry.ValueName is { } v ? $" <{v}>" : string.Empty;
        return $"{shortPart}{entry.LongNames[0]}{value}";
    }

    private static string HighlightSearch(string ansiLine, string query)
    {
        var plain = AnsiText.Strip(ansiLine);
        if (plain.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return $"{Palette.Cyan.Fg()}●{Rgb.Reset} {ansiLine}";
        }

        return $"  {ansiLine}";
    }
}
