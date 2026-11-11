using System;
using System.Globalization;
using System.IO;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

internal sealed class LiveProgress
{
    private const int BarWidth = 20;
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);

    private readonly TextWriter _output;
    private readonly Theme _theme;
    private readonly TimeProvider _timeProvider;
    private readonly GlyphSet _glyphs;
    private long _lastDraw;
    private bool _hasDrawn;
    private int _drawCount;

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

        _lastDraw = now;
        _hasDrawn = true;

        var spinnerIndex = (int)((now / Math.Max(1, _timeProvider.TimestampFrequency / 10)) % _glyphs.Spinner.Length);
        var spinner = _glyphs.Spinner[spinnerIndex];
        var first = $"{spinner} [{index.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}] {Sanitizer.ForDisplay(name)}";
        var normalized = fraction.HasValue ? Math.Clamp(fraction.Value, 0, 1) : (double?)null;
        var filled = normalized.HasValue
            ? Math.Clamp((int)Math.Round(BarWidth * normalized.Value, MidpointRounding.AwayFromZero), 0, BarWidth)
            : 0;
        var bar = Repeat(_glyphs.BarFull, filled) + Repeat(_glyphs.BarEmpty, BarWidth - filled);
        var percent = normalized.HasValue
            ? $"{Math.Round(normalized.Value * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%"
            : "--%";
        var speedText = speed.HasValue ? $"{speed.Value.ToString("0.0", CultureInfo.InvariantCulture)}x" : "--x";
        var second = $"{bar}  {percent}  {speedText}  elapsed {HumanFormat.Time(elapsed)}";
        if (normalized is > 0 and < 1)
        {
            var etaSeconds = elapsed.TotalSeconds * (1 - normalized.Value) / normalized.Value;
            var eta = TimeSpan.FromSeconds(Math.Min(etaSeconds, TimeSpan.MaxValue.TotalSeconds));
            second += $"  eta {HumanFormat.Time(eta)}";
        }

        if (_theme.Colors)
        {
            first = $"\u001b[36m{first}\u001b[0m";
            second = $"\u001b[32m{second}\u001b[0m";
        }

        if (_drawCount > 0)
        {
            _output.Write("\r\u001b[2K\u001b[1A\r\u001b[2K");
        }

        _output.WriteLine(first);
        _output.Write(second);
        _output.Flush();
        _drawCount++;
    }

    public void Clear()
    {
        if (!_theme.CanRedraw || _drawCount == 0)
        {
            return;
        }

        _output.Write("\r\u001b[2K\u001b[1A\r\u001b[2K\r");
        _output.Flush();
        _drawCount = 0;
        _hasDrawn = false;
    }

    private static string Repeat(string value, int count) =>
        count <= 0 ? string.Empty : string.Concat(System.Linq.Enumerable.Repeat(value, count));
}
