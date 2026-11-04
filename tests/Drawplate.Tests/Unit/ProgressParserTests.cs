using System;

using Drawplate.Models.Progress;

using Xunit;

namespace Drawplate.Tests.Unit;

public class ProgressParserTests
{
    [Fact]
    public void Block_WithUs_ReturnsElapsed()
    {
        var parser = new ProgressParser();
        Assert.Null(parser.Feed("out_time_us=5000000"));
        Assert.Null(parser.Feed("speed=1.5x"));
        var sample = parser.Feed("progress=continue");
        Assert.NotNull(sample);
        Assert.Equal(TimeSpan.FromSeconds(5), sample.Value.Elapsed);
        Assert.Equal(1.5, sample.Value.Speed);
        Assert.False(sample.Value.Ended);
    }

    [Fact]
    public void Block_WithOnlyMs_UsesMsAsMicroseconds()
    {
        var parser = new ProgressParser();
        Assert.Null(parser.Feed("out_time_ms=2500000"));
        var sample = parser.Feed("progress=end");
        Assert.NotNull(sample);
        Assert.Equal(TimeSpan.FromSeconds(2.5), sample.Value.Elapsed);
        Assert.True(sample.Value.Ended);
    }

    [Fact]
    public void Block_NaValues_ReturnsZeroElapsedNoSpeed()
    {
        var parser = new ProgressParser();
        Assert.Null(parser.Feed("out_time_us=N/A"));
        Assert.Null(parser.Feed("speed=N/A"));
        var sample = parser.Feed("progress=end");
        Assert.NotNull(sample);
        Assert.Equal(TimeSpan.Zero, sample.Value.Elapsed);
        Assert.Null(sample.Value.Speed);
        Assert.True(sample.Value.Ended);
    }

    [Fact]
    public void End_SetsEnded()
    {
        var parser = new ProgressParser();
        var cont = parser.Feed("progress=continue");
        Assert.False(cont!.Value.Ended);
        var end = parser.Feed("progress=end");
        Assert.True(end!.Value.Ended);
    }

    [Fact]
    public void TwoBlocks_AreIndependent()
    {
        var parser = new ProgressParser();
        parser.Feed("out_time_us=1000000");
        var first = parser.Feed("progress=end");
        Assert.Equal(TimeSpan.FromSeconds(1), first!.Value.Elapsed);

        parser.Feed("out_time_us=2000000");
        var second = parser.Feed("progress=end");
        Assert.Equal(TimeSpan.FromSeconds(2), second!.Value.Elapsed);
    }

    [Fact]
    public void Fraction_NullDuration_IsNull()
    {
        var sample = new ProgressSample(TimeSpan.FromSeconds(5), null, false);
        Assert.Null(ProgressParser.Fraction(sample, null));
    }

    [Fact]
    public void Fraction_ZeroDuration_IsNull()
    {
        var sample = new ProgressSample(TimeSpan.FromSeconds(5), null, false);
        Assert.Null(ProgressParser.Fraction(sample, TimeSpan.Zero));
    }

    [Fact]
    public void Fraction_Clamps()
    {
        var sample = new ProgressSample(TimeSpan.FromSeconds(10), null, false);
        Assert.Equal(1.0, ProgressParser.Fraction(sample, TimeSpan.FromSeconds(5)));
        Assert.Equal(0.5, ProgressParser.Fraction(sample, TimeSpan.FromSeconds(20)));
        Assert.Equal(0.0, ProgressParser.Fraction(new ProgressSample(TimeSpan.Zero, null, false), TimeSpan.FromSeconds(10)));
    }
}
