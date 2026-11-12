namespace Drawplate.Console.Ui;

/// <summary>Drawplate Aurora theme: emerald green, electric cyan, and ocean blue gradients with semantic status accents.</summary>
internal static class Palette
{
    public static readonly Rgb[] Aurora =
    [
        new(0x10, 0xB9, 0x81), // Emerald Green
        new(0x14, 0xB8, 0xA6), // Jade Teal
        new(0x06, 0xB6, 0xD4), // Electric Cyan
        new(0x0E, 0xA5, 0xE9), // Sky Blue
        new(0x3B, 0x82, 0xF6), // Ocean Blue
    ];

    public static readonly Rgb[] AuroraBright =
    [
        new(0x34, 0xD3, 0x99), // Mint
        new(0x2D, 0xD4, 0xBF), // Aqua Teal
        new(0x22, 0xD3, 0xEE), // Bright Cyan
        new(0x38, 0xBD, 0xF8), // Ice Sky
        new(0x60, 0xA5, 0xFA), // Soft Cobalt
    ];

    public static readonly Rgb[] SuccessRamp =
    [
        new(0x05, 0x96, 0x69),
        new(0x10, 0xB9, 0x81),
        new(0x34, 0xD3, 0x99),
        new(0x6E, 0xE7, 0xB7),
    ];

    public static readonly Rgb[] WarnRamp =
    [
        new(0xD9, 0x77, 0x06),
        new(0xF5, 0x9E, 0x0B),
        new(0xFB, 0xBF, 0x24),
    ];

    public static readonly Rgb Emerald = new(0x34, 0xD3, 0x99);
    public static readonly Rgb Cyan = new(0x22, 0xD3, 0xEE);
    public static readonly Rgb Sky = new(0x38, 0xBD, 0xF8);
    public static readonly Rgb Blue = new(0x60, 0xA5, 0xFA);

    public static readonly Rgb BorderPrimary = new(0x08, 0x91, 0xB2);
    public static readonly Rgb BorderSecondary = new(0x03, 0x69, 0xA1);
    public static readonly Rgb BorderSubtle = new(0x1E, 0x4E, 0x68);
    public static readonly Rgb Track = new(0x1E, 0x29, 0x3B);

    public static readonly Rgb TextPrimary = new(0xF1, 0xF5, 0xF9);
    public static readonly Rgb TextSecondary = new(0x94, 0xA3, 0xB8);
    public static readonly Rgb TextMuted = new(0x64, 0x74, 0x8B);

    public static readonly Rgb Warn = new(0xFB, 0xBF, 0x24);
    public static readonly Rgb Error = new(0xF8, 0x71, 0x71);

    public static readonly Rgb KeyBg = new(0x0C, 0x4A, 0x6E);
    public static readonly Rgb KeyFg = new(0xE0, 0xF2, 0xFE);
    public static readonly Rgb StatusBg = new(0x0B, 0x3B, 0x56);
    public static readonly Rgb StatusFg = new(0xEC, 0xFE, 0xFF);
}
