using System.Collections.Immutable;

using Drawplate.Models.Encoding;

namespace Drawplate.Models.Cli;

public sealed class ConvertOptions
{
    public required EncoderKind? Encoder { get; init; }
    public required int Quality { get; init; }
    public required SizeCap MaxSize { get; init; }
    public required string? InputFolder { get; init; }
    public required ImmutableArray<string> Files { get; init; }
    public required bool NoTimestamps { get; init; }
}
