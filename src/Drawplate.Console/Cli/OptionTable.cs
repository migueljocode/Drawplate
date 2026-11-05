using System;
using System.Collections.Immutable;

using Drawplate.Models.Cli;

namespace Drawplate.Console.Cli;

internal sealed record OptionEntry
{
    public required string ShortName { get; init; }
    public required ImmutableArray<string> LongNames { get; init; }
    public required bool TakesValue { get; init; }
    public required Action<RawOptions, string> Setter { get; init; }
}

internal static class OptionTable
{
    internal static ImmutableArray<OptionEntry> Entries { get; } =
    [
        new() { ShortName = "-i", LongNames = ["--input", "--input-dir"], TakesValue = true, Setter = (o, v) => o.InputFolder = v },
        new() { ShortName = "-o", LongNames = ["--output", "--output-dir"], TakesValue = true, Setter = (o, v) => { } },
        new() { ShortName = "-q", LongNames = ["--quality"], TakesValue = true, Setter = (o, v) => o.Quality = v },
        new() { ShortName = "-s", LongNames = ["--max-size"], TakesValue = true, Setter = (o, v) => o.MaxSize = v },
        new() { ShortName = "-e", LongNames = ["--encoder"], TakesValue = true, Setter = (o, v) => o.Encoder = v },
        new() { ShortName = "-c", LongNames = ["--cpu"], TakesValue = false, Setter = (o, v) => o.Cpu = true },
        new() { ShortName = "-t", LongNames = ["--no-timestamps"], TakesValue = false, Setter = (o, v) => o.NoTimestamps = true },
        new() { ShortName = "-l", LongNames = ["--list-encoders", "--list"], TakesValue = false, Setter = (o, v) => o.ListEncoders = true },
        new() { ShortName = "-m", LongNames = ["--interactive"], TakesValue = false, Setter = (o, v) => o.Interactive = true },
        new() { ShortName = "-y", LongNames = ["--install-deps", "--install"], TakesValue = false, Setter = (o, v) => o.InstallDeps = true },
        new() { ShortName = "-n", LongNames = ["--no-install"], TakesValue = false, Setter = (o, v) => o.NoInstall = true },
        new() { ShortName = "-h", LongNames = ["--help"], TakesValue = false, Setter = (o, v) => o.Help = true },
        new() { ShortName = "-V", LongNames = ["--version"], TakesValue = false, Setter = (o, v) => o.Version = true },
        new() { ShortName = "", LongNames = ["--ffmpeg-path"], TakesValue = true, Setter = (o, v) => o.FfmpegPath = v },
        new() { ShortName = "", LongNames = ["--verbose"], TakesValue = false, Setter = (o, v) => o.Verbose = true },
    ];
}
