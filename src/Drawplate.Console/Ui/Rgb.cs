using System;
using System.Globalization;

namespace Drawplate.Console.Ui;

/// <summary>24-bit RGB color with linear interpolation and ANSI SGR escape formatting.</summary>
internal readonly record struct Rgb(byte R, byte G, byte B)
{
    public const string Reset = "\u001b[0m";

    public static Rgb Lerp(Rgb a, Rgb b, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        return new Rgb(
            (byte)Math.Round(a.R + ((b.R - a.R) * clamped)),
            (byte)Math.Round(a.G + ((b.G - a.G) * clamped)),
            (byte)Math.Round(a.B + ((b.B - a.B) * clamped)));
    }

    public Rgb Brighten(double amount)
    {
        var factor = Math.Clamp(amount, 0.0, 1.0);
        return Lerp(this, new Rgb(255, 255, 255), factor);
    }

    public string Fg() => string.Create(CultureInfo.InvariantCulture, $"\u001b[38;2;{R};{G};{B}m");

    public string Bg() => string.Create(CultureInfo.InvariantCulture, $"\u001b[48;2;{R};{G};{B}m");

    public string Paint(string text) => string.Concat(Fg(), text, Reset);
}
