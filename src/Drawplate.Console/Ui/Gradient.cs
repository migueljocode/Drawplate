using System;
using System.Text;

namespace Drawplate.Console.Ui;

/// <summary>Fluid time-based Aurora gradients, cubic-eased diagonal reveals, sub-cell progress bars, and motion helpers.</summary>
internal static class Gradient
{
    private const string Eighths = " ▏▎▍▌▋▊▉";
    private const string SpinnerFrames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    public static Rgb Sample(ReadOnlySpan<Rgb> stops, double t)
    {
        if (stops.Length == 0)
        {
            return Palette.Cyan;
        }

        if (stops.Length == 1)
        {
            return stops[0];
        }

        var clamped = Math.Clamp(double.IsNaN(t) ? 0.0 : t, 0.0, 1.0);
        var scaled = clamped * (stops.Length - 1);
        var index = Math.Min((int)scaled, stops.Length - 2);
        return Rgb.Lerp(stops[index], stops[index + 1], scaled - index);
    }

    /// <summary>Cosine-smoothed cyclic Aurora wave so color transitions flow seamlessly back and forth without sharp seams.</summary>
    public static Rgb Wave(ReadOnlySpan<Rgb> stops, double position, double phase)
    {
        var raw = position + phase;
        var wrapped = raw - Math.Floor(raw);
        var pingPong = 1.0 - Math.Abs((2.0 * wrapped) - 1.0);
        var smooth = 0.5 - (0.5 * Math.Cos(Math.PI * pingPong));
        return Sample(stops, smooth);
    }

    public static char SpinnerChar(int frame) =>
        SpinnerFrames[Math.Abs(frame) % SpinnerFrames.Length];

    public static char SpinnerAt(double timeSeconds)
    {
        var idx = (int)Math.Floor(Math.Max(0.0, timeSeconds) * 14.0);
        return SpinnerFrames[idx % SpinnerFrames.Length];
    }

    /// <summary>Frame-rate independent exponential smoothing for progress bars.</summary>
    public static double Glide(double shown, double target, double dtSeconds, double rate = 9.0) =>
        shown + ((target - shown) * (1.0 - Math.Exp(-Math.Max(0.0, dtSeconds) * rate)));

    /// <summary>Cubic ease-out (0..1) for smooth reveal animations.</summary>
    public static double EaseOutCubic(double t)
    {
        var x = Math.Clamp(t, 0.0, 1.0);
        var inv = 1.0 - x;
        return 1.0 - (inv * inv * inv);
    }

    /// <summary>Discrete-frame overload retained for compatibility; delegates to continuous time-based rendering.</summary>
    public static string Text(string line, ReadOnlySpan<Rgb> stops, int frame = 0, double reveal = 1.0, double rowOffset = 0.0) =>
        TextAt(line, stops, frame * 0.032, reveal, rowOffset);

    /// <summary>Smooth 2D diagonal animated Aurora gradient text with cubic-eased reveal and soft traveling highlight.</summary>
    public static string TextAt(string line, ReadOnlySpan<Rgb> stops, double timeSeconds, double reveal = 1.0, double rowOffset = 0.0)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var easedReveal = EaseOutCubic(reveal);
        var phase = timeSeconds * 0.28;
        var shimmerCenter = ((timeSeconds * 0.42) % 1.8) - 0.4;
        var sb = new StringBuilder(line.Length * 22);
        var denom = Math.Max(1, line.Length - 1);

        for (var i = 0; i < line.Length; i++)
        {
            var x = (double)i / denom;
            var ch = line[i];
            var threshold = x * 0.88;
            if (easedReveal < 1.0 && threshold > easedReveal)
            {
                sb.Append(' ');
                continue;
            }

            if (ch == ' ')
            {
                sb.Append(' ');
                continue;
            }

            // Smooth cosine wave flowing diagonally across the logo
            var baseColor = Wave(stops, (x * 0.72) + rowOffset, -phase);

            // Soft specular aurora sheen sweeping periodically across the glyphs
            var sheenDist = Math.Abs(x - shimmerCenter + (rowOffset * 0.45));
            if (sheenDist < 0.18)
            {
                var sheenBoost = (0.5 + (0.5 * Math.Cos(Math.PI * (sheenDist / 0.18)))) * 0.38;
                baseColor = Rgb.Lerp(baseColor, Palette.MintGlow, sheenBoost);
            }

            // Smooth fade-in glow along the reveal wavefront instead of hard clipping
            if (easedReveal < 0.999)
            {
                var frontDist = easedReveal - threshold;
                if (frontDist < 0.14)
                {
                    var alpha = Math.Clamp(frontDist / 0.14, 0.0, 1.0);
                    var crest = Rgb.Lerp(Palette.DeepTeal, Palette.MintGlow, alpha);
                    baseColor = Rgb.Lerp(crest, baseColor, alpha);
                }
            }

            sb.Append(baseColor.Fg()).Append(ch);
        }

        sb.Append(Rgb.Reset);
        return sb.ToString();
    }

    /// <summary>Sub-cell 1/8th-block animated gradient bar using frame index.</summary>
    public static string Bar(int width, double fraction, ReadOnlySpan<Rgb> stops, int frame = -1, bool utf8 = true) =>
        BarAt(width, fraction, stops, frame < 0 ? -1.0 : frame * 0.032, utf8);

    /// <summary>Sub-cell 1/8th-block animated gradient bar driven by continuous elapsed seconds.</summary>
    public static string BarAt(int width, double fraction, ReadOnlySpan<Rgb> stops, double timeSeconds, bool utf8 = true)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var clamped = Math.Clamp(double.IsNaN(fraction) ? 0.0 : fraction, 0.0, 1.0);
        if (!utf8)
        {
            var filledAscii = Math.Clamp((int)Math.Round(clamped * width, MidpointRounding.AwayFromZero), 0, width);
            return BuildAsciiBar(width, filledAscii, stops, timeSeconds);
        }

        var eighths = (int)Math.Round(clamped * width * 8.0, MidpointRounding.AwayFromZero);
        var full = Math.Min(width, eighths / 8);
        var part = full < width ? eighths % 8 : 0;
        var phase = timeSeconds < 0 ? 0.0 : timeSeconds * 0.42;
        var shimmerPos = timeSeconds < 0 ? -1.0 : ((timeSeconds * 0.72) % 1.6) - 0.3;

        var sb = new StringBuilder(width * 24);
        for (var x = 0; x < full; x++)
        {
            var pos = width <= 1 ? 0.0 : (double)x / (width - 1);
            var color = ShimmerColor(stops, pos, phase, shimmerPos);
            sb.Append(color.Fg()).Append('█');
        }

        var cells = full;
        if (part > 0 && cells < width)
        {
            var pos = width <= 1 ? 0.0 : (double)cells / (width - 1);
            var color = ShimmerColor(stops, pos, phase, shimmerPos);
            sb.Append(color.Fg()).Append(Palette.Track.Bg()).Append(Eighths[part]).Append(Rgb.Reset);
            cells++;
        }

        if (cells < width)
        {
            sb.Append(Palette.Track.Fg()).Append(new string('░', width - cells));
        }

        sb.Append(Rgb.Reset);
        return sb.ToString();
    }

    /// <summary>Frame-indexed indeterminate comet scanner.</summary>
    public static string IndeterminateBar(int width, int frame, bool utf8 = true) =>
        IndeterminateBarAt(width, Math.Abs(frame) * 0.032, utf8);

    /// <summary>Smooth continuous cyan-emerald comet scanner for indeterminate/probing states.</summary>
    public static string IndeterminateBarAt(int width, double timeSeconds, bool utf8 = true)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var span = Math.Max(1.0, width - 1.0);
        var t = (Math.Max(0.0, timeSeconds) * 1.15) % 2.0;
        var tri = t <= 1.0 ? t : 2.0 - t;
        var smooth = 0.5 - (0.5 * Math.Cos(Math.PI * tri));
        var head = smooth * span;

        var sb = new StringBuilder(width * 20);
        for (var x = 0; x < width; x++)
        {
            var dist = Math.Abs(x - head);
            if (dist <= 3.2)
            {
                var intensity = Math.Clamp(1.0 - (dist / 3.2), 0.0, 1.0);
                var color = Wave(Palette.AuroraBright, (double)x / Math.Max(1, width - 1), -timeSeconds * 0.35);
                color = Rgb.Lerp(Palette.Track, color, intensity);
                var ch = utf8
                    ? (dist < 0.75 ? '█' : dist < 1.65 ? '▓' : '▒')
                    : (dist < 0.8 ? '=' : '-');
                sb.Append(color.Fg()).Append(ch);
            }
            else
            {
                sb.Append(Palette.Track.Fg()).Append(utf8 ? '─' : '-');
            }
        }

        sb.Append(Rgb.Reset);
        return sb.ToString();
    }

    private static string BuildAsciiBar(int width, int filled, ReadOnlySpan<Rgb> stops, double timeSeconds)
    {
        var sb = new StringBuilder(width * 18);
        var phase = timeSeconds < 0 ? 0.0 : timeSeconds * 0.42;
        for (var x = 0; x < width; x++)
        {
            if (x < filled)
            {
                var pos = width <= 1 ? 0.0 : (double)x / (width - 1);
                var color = Wave(stops, pos, -phase);
                sb.Append(color.Fg()).Append('#');
            }
            else
            {
                sb.Append(Palette.Track.Fg()).Append('-');
            }
        }

        sb.Append(Rgb.Reset);
        return sb.ToString();
    }

    private static Rgb ShimmerColor(ReadOnlySpan<Rgb> stops, double pos, double phase, double shimmerPos)
    {
        var baseColor = Wave(stops, pos, -phase);
        if (shimmerPos < 0)
        {
            return baseColor;
        }

        var dist = Math.Abs(pos - shimmerPos);
        if (dist >= 0.16)
        {
            return baseColor;
        }

        var boost = (0.5 + (0.5 * Math.Cos(Math.PI * (dist / 0.16)))) * 0.48;
        return baseColor.Brighten(boost);
    }
}
