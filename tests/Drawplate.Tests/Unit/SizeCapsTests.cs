using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class SizeCapsTests
{
    [Fact]
    public void VideoFilter_None_IsEvenScaleOnly()
    {
        Assert.Equal("scale=trunc(iw/2)*2:trunc(ih/2)*2", SizeCaps.VideoFilter(SizeCap.None));
    }

    [Fact]
    public void VideoFilter_Hd_EqualsGolden()
    {
        var expected =
            "scale=w='if(gt(a,1),min(iw,1280),min(iw,720))':h='if(gt(a,1),min(ih,720),min(ih,1280))':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2";
        Assert.Equal(expected, SizeCaps.VideoFilter(SizeCap.Hd));
    }

    [Fact]
    public void VideoFilter_Sd_UsesBox854x480()
    {
        var expected =
            "scale=w='if(gt(a,1),min(iw,854),min(iw,480))':h='if(gt(a,1),min(ih,480),min(ih,854))':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2";
        Assert.Equal(expected, SizeCaps.VideoFilter(SizeCap.Sd));
    }

    [Theory]
    [InlineData("HD")]
    [InlineData("FullHD")]
    [InlineData("4K")]
    public void TryParse_CaseInsensitive(string text)
    {
        Assert.True(SizeCaps.TryParse(text, out var cap));
        Assert.NotEqual(SizeCap.None, cap);
    }

    [Theory]
    [InlineData("8k")]
    [InlineData("720")]
    public void TryParse_Rejects(string text)
    {
        Assert.False(SizeCaps.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_EmptyAndNone_AreNone()
    {
        Assert.True(SizeCaps.TryParse(null, out var a));
        Assert.Equal(SizeCap.None, a);
        Assert.True(SizeCaps.TryParse("", out var b));
        Assert.Equal(SizeCap.None, b);
        Assert.True(SizeCaps.TryParse("none", out var c));
        Assert.Equal(SizeCap.None, c);
    }
}
