using System.Collections.Immutable;

namespace Drawplate.Models.Cli;

public sealed record RawOptions
{
    public string? Encoder { get; set; } = "auto";
    public string? Quality { get; set; } = "50";
    public string? MaxSize { get; set; }
    public string? InputFolder { get; set; } = ".";
    public ImmutableArray<string> Files { get; set; } = [];
    public bool NoTimestamps { get; set; }
    public bool Cpu { get; set; }
    public bool ListEncoders { get; set; }
    public bool Interactive { get; set; }
    public bool InstallDeps { get; set; }
    public bool NoInstall { get; set; }
    public bool Help { get; set; }
    public bool Version { get; set; }
    public string? FfmpegPath { get; set; }
    public bool Verbose { get; set; }
}
