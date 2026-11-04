using System.Collections.Immutable;

namespace Drawplate.Models.Formatting;

public sealed record GlyphSet
{
    public required string Ok { get; init; }
    public required string Fail { get; init; }
    public required string Skip { get; init; }
    public required string Arrow { get; init; }
    public required string Down { get; init; }
    public required string Up { get; init; }
    public required string Separator { get; init; }
    public required string BarFull { get; init; }
    public required string BarEmpty { get; init; }
    public required ImmutableArray<string> Spinner { get; init; }
}

public static class Glyphs
{
    public static readonly GlyphSet Utf8 = new()
    {
        Ok = "✓",
        Fail = "✗",
        Skip = "–",
        Arrow = "→",
        Down = "↓",
        Up = "↑",
        Separator = "·",
        BarFull = "█",
        BarEmpty = "░",
        Spinner = ImmutableArray.Create(["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"]),
    };

    public static readonly GlyphSet Ascii = new()
    {
        Ok = "+",
        Fail = "x",
        Skip = "-",
        Arrow = "->",
        Down = "v",
        Up = "^",
        Separator = ".",
        BarFull = "#",
        BarEmpty = "-",
        Spinner = ImmutableArray.Create(["|", "/", "-", "\\"]),
    };
}
