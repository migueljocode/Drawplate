using System;
using System.Linq;

using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class EncoderTests
{
    [Theory]
    [InlineData("Linux", "nvenc,qsv,vaapi,cpu")]
    [InlineData("Windows", "nvenc,qsv,amf,cpu")]
    [InlineData("macOS", "videotoolbox,cpu")]
    public void Sets_PerOs(string osName, string expected)
    {
        var os = osName switch
        {
            "Linux" => OperatingSystemKind.Linux,
            "Windows" => OperatingSystemKind.Windows,
            _ => OperatingSystemKind.MacOS,
        };
        var ids = string.Join(",", EncoderSets.For(os).Select(EncoderSets.Id));
        Assert.Equal(expected, ids);
    }

    [Fact]
    public void Parse_WrongOs_Amf_OnLinux()
    {
        var result = EncoderSets.Parse("amf", OperatingSystemKind.Linux);
        Assert.Equal(ParseStatus.WrongOs, result.Status);
    }

    [Fact]
    public void Parse_UnknownEverywhere_Foo()
    {
        var result = EncoderSets.Parse("foo", OperatingSystemKind.Linux);
        Assert.Equal(ParseStatus.UnknownEverywhere, result.Status);
    }

    [Fact]
    public void Args_Cpu_Q50()
    {
        var args = EncoderProfiles.Args(EncoderKind.Cpu, 50);
        Assert.Equal(["-c:v", "libx264", "-crf", "26", "-preset", "medium", "-pix_fmt", "yuv420p"], args.ToArray());
    }

    [Fact]
    public void Args_Nvenc_Q80_UsesCrf11()
    {
        var args = EncoderProfiles.Args(EncoderKind.Nvenc, 80);
        Assert.Contains("-cq", args);
        var idx = Array.IndexOf(args.ToArray(), "-cq");
        Assert.Equal("11", args[idx + 1]);
    }

    [Fact]
    public void Args_VideoToolbox_UsesQualityDirectly()
    {
        var args = EncoderProfiles.Args(EncoderKind.VideoToolbox, 50);
        Assert.Contains("-q:v", args);
        var idx = Array.IndexOf(args.ToArray(), "-q:v");
        Assert.Equal("50", args[idx + 1]);
    }

    [Fact]
    public void Badge_KnownValues()
    {
        Assert.Equal("[CPU]", EncoderSets.Badge(EncoderKind.Cpu));
        Assert.Equal("[NVENC]", EncoderSets.Badge(EncoderKind.Nvenc));
        Assert.Equal("[VT]", EncoderSets.Badge(EncoderKind.VideoToolbox));
    }
}
