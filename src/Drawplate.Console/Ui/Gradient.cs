using System;
using System.Text;

namespace Drawplate.Console.Ui;

/// <summary>Truecolor animated gradients, sub-cell progress bars, and frame-rate independent motion helpers.</summary>
internal static class Gradient
{
    private const string Eighths = " ▏▎▍▌▋▊▉";
    private const string Utf8Spinner = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
    private const string AsciiSpinner = "|/-\\";

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

        var clamped = Math.Clamp(double.IsNaN(t) ? 0.0 : t, 0.0, 1.0) * (stops.Length - 1);
        var index = Math.Min((int)clamped, stops.Length - 2);
        return Rgb.Lerp(stops[index], stops[index + 1], clamped - index);
    }

    /// <summary>Smooth ping-pong wave sampling across gradient stops.</summary>
    public static Rgb Wave(ReadOnlySpan<Rgb> stops, double position, double phase)
    {
        var u = position + phase;
        var triangle = 1.0 - Math.Abs((2.0 * (u - Math.Floor(u))) - 1.0);
        return Sample(stops, triangle);
    }

    public static char SpinnerChar(int frame, bool utf8)
    {
        var set = utf8 ? Utf8Spinner : AsciiSpinner;
        return set[Math.Abs(frame) % set.Length];
    }

    /// <summary>Frame-rate independent exponential smoothing toward <paramref name="target"/>.</summary>
    public static double Glide(double shown, double target, double dtSeconds, double rate = 8.0)
    {
        var clampedTarget = Math.Clamp(double.IsNaN(target) ? 0.0 : target, 0.0, 1.0);
        var clampedShown = Math.Clamp(double.IsNaN(shown) ? 0.0 : shown, 0.0, 1.0);
        if (dtSeconds <= 0)
        {
            return clampedShown;
        }

        return clampedShown + ((clampedTarget - clampedShown) * (1.0 - Math.Exp(-dtSeconds * rate)));
    }

    /// <summary>Per-character animated gradient text with optional left-to-right reveal wipe and diagonal row offset.</summary>
    public static string Text(string line, ReadOnlySpan<Rgb> stops, int frame = 0, double reveal = 1.0, double rowOffset = 0.0)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(line.Length * 20);
        var phase = frame * 0.025;
        var length = Math.Max(1, line.Length);
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            var x = (double)i / length;
            if (x > reveal)
            {
                sb.Append(' ');
                continue;
            }

            if (ch == ' ')
            {
                sb.Append(' ');
                continue;
            }

            var color = Wave(stops, (x * 0.85) + rowOffset, -phase);
            if (reveal < 1.0 && reveal - x < 0.08)
            {
                color = color.Brighten(0.55);
            }

            sb.Append(color.Fg()).Append(ch);
        }

        sb.Append(Rgb.Reset);
        return sb.ToString();
    }

    /// <summary>Sub-cell 1/8th-block animated gradient bar with traveling specular shimmer.</summary>
    public static string Bar(int width, double fraction, ReadOnlySpan<Rgb> stops, int frame = -1, bool utf8 = true)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var clamped = Math.Clamp(double.IsNaN(fraction) ? 0.0 : fraction, 0.0, 1.0);
        if (!utf8)
        {
            var filledAscii = Math.Clamp((int)Math.Round(clamped * width, MidpointRounding.AwayFromZero), 0, width);
            return BuildAsciiBar(width, filledAscii, stops, frame);
        }

        var eighths = (int)Math.Round(clamped * width * 8.0, MidpointRounding.AwayFromZero);
        var full = Math.Min(width, eighths / 8);
        var part = full < width ? eighths % 8 : 0;
        var phase = frame < 0 ? 0.0 : frame * 0.03;
        var shimmerPos = frame < 0 ? -1.0 : ((frame * 0.055) % 1.6) - 0.3;

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

    /// <summary>Bouncing cyan-emerald comet scanner for indeterminate progress.</summary>
    public static string IndeterminateBar(int width, int frame, bool utf8 = true)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var span = Math.Max(1, width - 1);
        var cycle = Math.Abs(frame) % (span * 2);
        var head = cycle <= span ? cycle : (span * 2) - cycle;
        var sb = new StringBuilder(width * 20);
        for (var x = 0; x < width; x++)
        {
            var dist = Math.Abs(x - head);
            if (dist <= 3)
            {
                var intensity = 1.0 - (dist / 4.0);
                var color = Sample(Palette.AuroraBright, (double)x / Math.Max(1, width - 1));
                color = Rgb.Lerp(Palette.Track, color, intensity);
                var ch = utf8 ? (dist == 0 ? '█' : dist == 1 ? '▓' : '▒') : (dist == 0 ? '=' : '-');
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

    private static string BuildAsciiBar(int width, int filled, ReadOnlySpan<Rgb> stops, int frame)
    {
        var sb = new StringBuilder(width * 18);
        var phase = frame < 0 ? 0.0 : frame * 0.03;
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
        if (dist >= 0.14)
        {
            return baseColor;
        }

        var boost = (1.0 - (dist / 0.14)) * 0.52;
        return baseColor.Brighten(boost);
    }
}
