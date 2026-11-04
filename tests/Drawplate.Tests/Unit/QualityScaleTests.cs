using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class QualityScaleTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(99, 2)]
    [InlineData(75, 14)]
    [InlineData(50, 26)]
    [InlineData(25, 39)]
    [InlineData(2, 50)]
    [InlineData(1, 51)]
    public void ToCrf_KnownPoints(int q, int expectedCrf)
    {
        Assert.Equal(expectedCrf, QualityScale.ToCrf(q));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void ToCrf_OutOfRange_Throws(int q)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => QualityScale.ToCrf(q));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("5x")]
    [InlineData(" 5")]
    [InlineData("+5")]
    [InlineData("5.0")]
    [InlineData("٥")]
    public void TryParse_Rejects(string text)
    {
        Assert.False(QualityScale.TryParse(text, out _));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("50")]
    [InlineData("100")]
    public void TryParse_Accepts(string text)
    {
        Assert.True(QualityScale.TryParse(text, out var q));
        Assert.InRange(q, 1, 100);
    }
}
