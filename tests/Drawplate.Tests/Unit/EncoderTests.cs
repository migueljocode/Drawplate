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
        var args = EncoderProfiles.For(EncoderKind.Cpu, 50).Encode;
        Assert.Equal(["-c:v", "libx264", "-crf", "26", "-preset", "medium", "-pix_fmt", "yuv420p"], args.ToArray());
    }

    [Fact]
    public void Args_Nvenc_Q80_UsesCrf11()
    {
        var args = EncoderProfiles.For(EncoderKind.Nvenc, 80).Encode;
        Assert.Contains("-cq", args);
        var idx = Array.IndexOf(args.ToArray(), "-cq");
        Assert.Equal("11", args[idx + 1]);
    }

    [Fact]
    public void Args_VideoToolbox_UsesQualityDirectly()
    {
        var args = EncoderProfiles.For(EncoderKind.VideoToolbox, 50).Encode;
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

    [Fact]
    public void Profile_Vaapi_WithoutTarget_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EncoderProfiles.For(EncoderKind.Vaapi, 50));
    }

    [Fact]
    public void Profile_Vaapi_SplitsPreInputFilterAndEncode()
    {
        var profile = EncoderProfiles.For(EncoderKind.Vaapi, 50, new VaapiTarget("/dev/dri/renderD128", false));
        Assert.Equal(["-vaapi_device", "/dev/dri/renderD128"], profile.PreInput.ToArray());
        Assert.Equal("format=nv12,hwupload", profile.FilterSuffix);
        Assert.Equal(["-c:v", "h264_vaapi", "-global_quality", "26"], profile.Encode.ToArray());
    }

    [Fact]
    public void Probe_EveryKindOfEveryOs_DoesNotThrow()
    {
        var target = new VaapiTarget("/dev/dri/renderD128", false);
        foreach (var os in Enum.GetValues<OperatingSystemKind>())
        {
            foreach (var kind in EncoderSets.For(os))
            {
                var vaapi = kind == EncoderKind.Vaapi ? target : null;
                Assert.NotEmpty(FfmpegArgv.Probe(kind, vaapi));
                Assert.NotEmpty(FfmpegArgv.Convert("SRC", "PARTIAL", kind, 50, SizeCap.None, vaapi));
            }
        }
    }
}
