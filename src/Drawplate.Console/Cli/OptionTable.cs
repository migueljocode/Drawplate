using System;
using System.Collections.Immutable;

using Drawplate.Models.Cli;

namespace Drawplate.Console.Cli;

internal sealed record OptionEntry
{
    /// <summary>Bare letter without the dash, or empty when the option has no short form.</summary>
    public required string ShortName { get; init; }
    public required ImmutableArray<string> LongNames { get; init; }
    /// <summary>Placeholder shown in help, or null when the option takes no value.</summary>
    public required string? ValueName { get; init; }
    public required string Help { get; init; }
    public required Action<RawOptions, string> Setter { get; init; }

    public bool TakesValue => ValueName is not null;
}

internal static class OptionTable
{
    internal static ImmutableArray<OptionEntry> Entries { get; } =
    [
        new() { ShortName = "i", LongNames = ["--input", "--input-dir"], ValueName = "folder", Help = "Folder that contains the videos (default: current folder)", Setter = (o, v) => o.InputFolder = v },
        new() { ShortName = "o", LongNames = ["--output", "--output-dir"], ValueName = "folder", Help = "Output folder (default: <input>/compressed, or ./compressed for files)", Setter = (o, v) => o.OutputFolder = v },
        new() { ShortName = "t", LongNames = ["--no-timestamps"], ValueName = null, Help = "Do not copy the original file dates", Setter = (o, _) => o.NoTimestamps = true },
        new() { ShortName = "q", LongNames = ["--quality"], ValueName = "1-100", Help = "Quality, higher is better (default: 50)", Setter = (o, v) => o.Quality = v },
        new() { ShortName = "s", LongNames = ["--max-size"], ValueName = "size", Help = "Largest output size: sd, hd, fullhd or 4k (default: none)", Setter = (o, v) => o.MaxSize = v },
        new() { ShortName = "e", LongNames = ["--encoder"], ValueName = "n", Help = "Encoder name or auto (default: auto); -l shows what works here", Setter = (o, v) => o.Encoder = v },
        new() { ShortName = "c", LongNames = ["--cpu"], ValueName = null, Help = "Use the CPU (libx264) only; same as -e cpu", Setter = (o, _) => o.Cpu = true },
        new() { ShortName = "l", LongNames = ["--list-encoders", "--list"], ValueName = null, Help = "Print the encoders that work on this machine, then exit", Setter = (o, _) => o.ListEncoders = true },
        new() { ShortName = "m", LongNames = ["--interactive"], ValueName = null, Help = "Interactive menu (needs a terminal)", Setter = (o, _) => o.Interactive = true },
        new() { ShortName = "y", LongNames = ["--install-deps", "--install"], ValueName = null, Help = "Run the FFmpeg install command without asking, then exit", Setter = (o, _) => o.InstallDeps = true },
        new() { ShortName = "n", LongNames = ["--no-install"], ValueName = null, Help = "Never offer or run an install (wins over -y)", Setter = (o, _) => o.NoInstall = true },
        new() { ShortName = "h", LongNames = ["--help"], ValueName = null, Help = "Show this help", Setter = (o, _) => o.Help = true },
        new() { ShortName = "V", LongNames = ["--version"], ValueName = null, Help = "Print the version", Setter = (o, _) => o.Version = true },
        new() { ShortName = "", LongNames = ["--ffmpeg-path"], ValueName = "file", Help = "Use this FFmpeg executable", Setter = (o, v) => o.FfmpegPath = v },
        new() { ShortName = "", LongNames = ["--verbose"], ValueName = null, Help = "Print ffmpeg arguments and stderr tails to stderr", Setter = (o, _) => o.Verbose = true },
    ];
}
