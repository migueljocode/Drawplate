using System;
using System.Globalization;

namespace Drawplate.Models.Progress;

public sealed class ProgressParser
{
    private long? _us;
    private long? _ms;
    private double? _speed;
    private bool _hasUs;

    public ProgressSample? Feed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            _hasUs = TryParseMicro(line.AsSpan(12), out var us);
            if (_hasUs)
            {
                _us = us;
            }
            return null;
        }

        if (line.StartsWith("out_time_ms=", StringComparison.Ordinal))
        {
            if (!_hasUs && TryParseMicro(line.AsSpan(12), out var ms))
            {
                _ms = ms;
            }
            return null;
        }

        if (line.StartsWith("speed=", StringComparison.Ordinal))
        {
            var value = line.AsSpan(6);
            if (!value.SequenceEqual("N/A") && value.Length > 1 && value[^1] == 'x'
                && double.TryParse(value[..^1], CultureInfo.InvariantCulture, out var s))
            {
                _speed = s;
            }
            return null;
        }

        if (line.StartsWith("progress=", StringComparison.Ordinal))
        {
            var ended = line.AsSpan(9).SequenceEqual("end");
            var elapsed = _hasUs
                ? TimeSpan.FromMicroseconds(_us!.Value)
                : (_ms.HasValue ? TimeSpan.FromMicroseconds(_ms.Value) : TimeSpan.Zero);
            var sample = new ProgressSample(elapsed, _speed, ended);
            Reset();
            return sample;
        }

        return null;
    }

    private void Reset()
    {
        _us = null;
        _ms = null;
        _speed = null;
        _hasUs = false;
    }

    private static bool TryParseMicro(ReadOnlySpan<char> text, out long value)
    {
        value = 0;
        if (text.SequenceEqual("N/A"))
        {
            return false;
        }
        return long.TryParse(text, CultureInfo.InvariantCulture, out value);
    }

    public static double? Fraction(ProgressSample sample, TimeSpan? duration)
    {
        if (duration is null || duration == TimeSpan.Zero)
        {
            return null;
        }
        var fraction = sample.Elapsed.TotalSeconds / duration.Value.TotalSeconds;
        if (fraction < 0) return 0;
        if (fraction > 1) return 1;
        return fraction;
    }
}
