using System.Collections.Immutable;

namespace Drawplate.Models.Cli;

public sealed record RawOptions
{
    public string? Encoder { get; init; } = "auto";
    public string? Quality { get; init; } = "50";
    public string? MaxSize { get; init; }
    public string? InputFolder { get; init; } = ".";
    public ImmutableArray<string> Files { get; init; } = [];
    public bool NoTimestamps { get; init; }
    public bool Cpu { get; init; }
    public bool ListEncoders { get; init; }
    public bool Interactive { get; init; }
    public bool InstallDeps { get; init; }
    public bool NoInstall { get; init; }
    public bool Help { get; init; }
    public bool Version { get; init; }
    public string? FfmpegPath { get; init; }
    public bool Verbose { get; init; }
}
