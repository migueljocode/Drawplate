using System.Collections.Immutable;

namespace Drawplate.Models.Encoding;

/// <summary>Encoder arguments split by where they go: before -i, appended to -vf, and after the filter.</summary>
public sealed record EncoderProfile(ImmutableArray<string> PreInput, string? FilterSuffix, ImmutableArray<string> Encode);
