using System;
using System.Globalization;
using System.IO;
using System.Text;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

internal sealed class LiveProgress
{
    private const int DefaultBarWidth = 28;
    private const int CompactBarWidth = 20;
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);

    private readonly TextWriter _output;
    private readonly Theme _theme;
    private readonly TimeProvider _timeProvider;
    private readonly GlyphSet _glyphs;
    private long _lastDraw;
    private bool _hasDrawn;
    private int _drawCount;
    private int _activeIndex = -1;
    private double _shownFraction;

    public LiveProgress(TextWriter output, Theme theme, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _output = output;
        _theme = theme;
        _timeProvider = timeProvider;
        _glyphs = theme.Utf8Glyphs ? Glyphs.Utf8 : Glyphs.Ascii;
    }

    public void Draw(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_theme.CanRedraw)
        {
            return;
        }

        var now = _timeProvider.GetTimestamp();
        if (_hasDrawn && _timeProvider.GetElapsedTime(_lastDraw, now) < MinimumInterval)
        {
            return;
        }

        var dt = _hasDrawn ? _timeProvider.GetElapsedTime(_lastDraw, now).TotalSeconds : 0.1;
        _lastDraw = now;
        _hasDrawn = true;

        if (_activeIndex != index)
        {
            _activeIndex = index;
            _shownFraction = 0.0;
        }

        var normalized = fraction.HasValue ? Math.Clamp(fraction.Value, 0.0, 1.0) : (double?)null;
        if (normalized.HasValue)
        {
            _shownFraction = normalized.Value <= 0.0
                ? 0.0
                : Gradient.Glide(_shownFraction, normalized.Value, Math.Max(0.05, dt), rate: 10.0);
        }

        var frame = _drawCount;
        var spinnerColor = Gradient.Wave(Palette.AuroraBright, (frame % 10) / 10.0, 0.0);
        var spinner = _glyphs.Spinner[Math.Abs(frame) % _glyphs.Spinner.Length];
        var safeName = Sanitizer.ForDisplay(name);
        var maxWidth = Math.Max(40, _theme.Width - 2);

        var countText = $"[{index.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}]";
        var firstPlain = $"{spinner} {countText} {safeName}";
        var first = _theme.Colors
            ? AnsiText.ClipVisible(
                $"{spinnerColor.Fg()}{spinner}{Rgb.Reset} {Palette.Sky.Fg()}{countText}{Rgb.Reset} {Palette.TextPrimary.Fg()}{safeName}{Rgb.Reset}",
                maxWidth)
            : AnsiText.ClipVisible(firstPlain, maxWidth);

        var barWidth = _theme.Colors && _theme.Utf8Glyphs && maxWidth >= 66 ? DefaultBarWidth : CompactBarWidth;
        string bar;
        if (_theme.Colors && _theme.Utf8Glyphs)
        {
            bar = normalized.HasValue
                ? Gradient.Bar(barWidth, _shownFraction, Palette.Aurora, frame, utf8: true)
                : Gradient.IndeterminateBar(barWidth, frame, utf8: true);
        }
        else if (_theme.Colors)
        {
            bar = normalized.HasValue
                ? Gradient.Bar(barWidth, normalized.Value, Palette.Aurora, frame, utf8: false)
                : new string('-', barWidth);
        }
        else
        {
            var filled = normalized.HasValue
                ? Math.Clamp((int)Math.Round(barWidth * normalized.Value, MidpointRounding.AwayFromZero), 0, barWidth)
                : 0;
            bar = Repeat(_glyphs.BarFull, filled) + Repeat(_glyphs.BarEmpty, barWidth - filled);
        }

        var percent = normalized.HasValue
            ? $"{Math.Round(normalized.Value * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%"
            : "--%";
        var speedText = speed.HasValue ? $"{speed.Value.ToString("0.0", CultureInfo.InvariantCulture)}x" : "--x";
        var elapsedText = HumanFormat.Time(elapsed);

        string? etaText = null;
        if (normalized is > 0 and < 1)
        {
            var etaSeconds = elapsed.TotalSeconds * (1 - normalized.Value) / normalized.Value;
            var eta = TimeSpan.FromSeconds(Math.Min(etaSeconds, TimeSpan.MaxValue.TotalSeconds));
            etaText = HumanFormat.Time(eta);
        }

        string second;
        if (_theme.Colors && _theme.Utf8Glyphs)
        {
            var sb = new StringBuilder(160);
            sb.Append(bar)
                .Append("  ")
                .Append(Palette.Cyan.Fg()).Append(percent.PadLeft(4)).Append(Rgb.Reset)
                .Append("  ")
                .Append(Palette.Emerald.Fg()).Append(speedText).Append(Rgb.Reset)
                .Append("  ")
                .Append(Palette.TextMuted.Fg()).Append("elapsed ").Append(Palette.TextSecondary.Fg()).Append(elapsedText).Append(Rgb.Reset);
            if (etaText is not null)
            {
                sb.Append("  ").Append(Palette.TextMuted.Fg()).Append("eta ").Append(Palette.Sky.Fg()).Append(etaText).Append(Rgb.Reset);
            }

            second = AnsiText.ClipVisible(sb.ToString(), maxWidth);
        }
        else if (_theme.Colors)
        {
            var sb = new StringBuilder(160);
            sb.Append(Palette.TextSecondary.Fg()).Append(bar).Append("  ").Append(percent).Append(Rgb.Reset)
                .Append("  ")
                .Append(Palette.Emerald.Fg()).Append(speedText).Append(Rgb.Reset)
                .Append("  ")
                .Append(Palette.TextMuted.Fg()).Append("elapsed ").Append(Palette.TextSecondary.Fg()).Append(elapsedText).Append(Rgb.Reset);
            if (etaText is not null)
            {
                sb.Append("  ").Append(Palette.TextMuted.Fg()).Append("eta ").Append(Palette.Sky.Fg()).Append(etaText).Append(Rgb.Reset);
            }

            second = AnsiText.ClipVisible(sb.ToString(), maxWidth);
        }
        else
        {
            second = $"{bar}  {percent}  {speedText}  elapsed {elapsedText}";
            if (etaText is not null)
            {
                second += $"  eta {etaText}";
            }

            second = AnsiText.ClipVisible(second, maxWidth);
        }

        var frameBuilder = new StringBuilder(256);
        if (_theme.Colors)
        {
            frameBuilder.Append("\u001b[?2026h");
        }

        if (_drawCount > 0)
        {
            frameBuilder.Append("\r\u001b[2K\u001b[1A\r\u001b[2K");
        }

        frameBuilder.Append(first).Append('\n').Append(second);
        if (_theme.Colors)
        {
            frameBuilder.Append("\u001b[K\u001b[?2026l");
        }

        _output.Write(frameBuilder.ToString());
        _output.Flush();
        _drawCount++;
    }

    public void Clear()
    {
        if (!_theme.CanRedraw || _drawCount == 0)
        {
            return;
        }

        _output.Write("\r\u001b[2K\u001b[1A\r\u001b[2K\r\u001b[0m");
        _output.Flush();
        _drawCount = 0;
        _hasDrawn = false;
    }

    private static string Repeat(string value, int count) =>
        count <= 0 ? string.Empty : string.Concat(System.Linq.Enumerable.Repeat(value, count));
}
