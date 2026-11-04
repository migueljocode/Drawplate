using System;

using Drawplate.Models.Formatting;

using Xunit;

namespace Drawplate.Tests.Unit;

public class FormattingTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(128210145, "122.3 MB")]
    public void Bytes_Theory(long bytes, string expected)
    {
        Assert.Equal(expected, HumanFormat.Bytes(bytes));
    }

    [Theory]
    [InlineData(65, "1:05")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3661, "1:01:01")]
    public void Time_Theory(int seconds, string expected)
    {
        Assert.Equal(expected, HumanFormat.Time(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Sanitizer_RemovesEsc()
    {
        var sanitized = Sanitizer.ForDisplay("hello\x1Bworld\x7Ftest");
        Assert.DoesNotContain("\x1B", sanitized);
        Assert.DoesNotContain("\x7F", sanitized);
        Assert.Equal("helloworldtest", sanitized);
    }

    [Fact]
    public void Clip_AddsMarker()
    {
        Assert.Equal("hello…", HumanFormat.Clip("hello world", 6));
        Assert.Equal("hello world", HumanFormat.Clip("hello world", 20));
    }
}
