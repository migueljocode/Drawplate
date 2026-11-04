using System;

using Drawplate.Models.Planning;

using Xunit;

namespace Drawplate.Tests.Unit;

public class InputRulesTests
{
    [Theory]
    [InlineData("a.MOV", true)]
    [InlineData("a.Mp4", true)]
    [InlineData("a.txt", false)]
    [InlineData(".mp4", false)]
    [InlineData("a", false)]
    public void IsSupported_Theory(string fileName, bool expected)
    {
        Assert.Equal(expected, InputRules.IsSupported(fileName));
    }

    [Theory]
    [InlineData(".hidden", true)]
    [InlineData("hidden.mp4", false)]
    [InlineData("..x", true)]
    public void IsHidden_Theory(string fileName, bool expected)
    {
        Assert.Equal(expected, InputRules.IsHidden(fileName));
    }

    [Fact]
    public void Order_CaseInsensitiveThenOrdinal()
    {
        var names = new[] { "B.mov", "a.mp4", "A.mp4" };
        Array.Sort(names, InputRules.NameOrder);
        Assert.Equal(["A.mp4", "a.mp4", "B.mov"], names);
    }
}
