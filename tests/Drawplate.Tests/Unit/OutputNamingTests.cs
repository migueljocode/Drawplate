using Drawplate.Models.Planning;

using Xunit;

namespace Drawplate.Tests.Unit;

public class OutputNamingTests
{
    [Theory]
    [InlineData("a.mov", "a.mp4")]
    [InlineData("a.b.mkv", "a.b.mp4")]
    [InlineData("x.MP4", "x.mp4")]
    public void TargetName_Theory(string input, string expected)
    {
        Assert.Equal(expected, OutputNaming.TargetName(input));
    }

    [Fact]
    public void PartialName_ContainsPidAndStartsWithDot()
    {
        var partial = OutputNaming.PartialName("My Clip.MOV", 4242);
        Assert.StartsWith(".", partial);
        Assert.Contains(".4242.", partial);
        Assert.EndsWith(".partial.mp4", partial);
        Assert.Equal(".My Clip.4242.partial.mp4", partial);
    }
}
