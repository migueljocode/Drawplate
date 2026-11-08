using System;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class GoldenArgvTests
{
    private static string[] ReadGolden(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(GoldenArgvTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoDir, "tests", "Drawplate.Tests", "Golden", name);
        return File.ReadAllLines(path);
    }

    [Fact]
    public void Convert_Cpu_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Cpu, 50, SizeCap.None);
        Assert.Equal(ReadGolden("cpu-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_Cpu_Q80_Hd_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Cpu, 80, SizeCap.Hd);
        Assert.Equal(ReadGolden("cpu-q80-hd.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_Nvenc_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Nvenc, 50, SizeCap.None);
        Assert.Equal(ReadGolden("nvenc-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_Qsv_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Qsv, 50, SizeCap.None);
        Assert.Equal(ReadGolden("qsv-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_Cpu_MatchesGolden()
    {
        var args = FfmpegArgv.Probe(EncoderKind.Cpu);
        Assert.Equal(ReadGolden("probe-cpu.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_Nvenc_MatchesGolden()
    {
        var args = FfmpegArgv.Probe(EncoderKind.Nvenc);
        Assert.Equal(ReadGolden("probe-nvenc.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_Qsv_MatchesGolden()
    {
        var args = FfmpegArgv.Probe(EncoderKind.Qsv);
        Assert.Equal(ReadGolden("probe-qsv.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_PathWithSpaces_IsSingleElement()
    {
        var args = FfmpegArgv.Convert("my video file.mov", "PARTIAL", EncoderKind.Cpu, 50, SizeCap.None);
        Assert.Contains("my video file.mov", args);
        Assert.Equal(1, args.Count(a => a == "my video file.mov"));
    }

    private static readonly VaapiTarget Vaapi = new("/dev/dri/renderD128", false);

    [Fact]
    public void Convert_Vaapi_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Vaapi, 50, SizeCap.None, Vaapi);
        Assert.Equal(ReadGolden("vaapi-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_VaapiLowPower_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Vaapi, 50, SizeCap.None, Vaapi with { LowPower = true });
        Assert.Equal(ReadGolden("vaapi-lp-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_Vaapi_MatchesGolden()
    {
        var args = FfmpegArgv.Probe(EncoderKind.Vaapi, Vaapi);
        Assert.Equal(ReadGolden("probe-vaapi.txt"), args.ToArray());
    }

    [Fact]
    public void Convert_VaapiHd_FilterIsCapThenEvenThenSuffix()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Vaapi, 50, SizeCap.Hd, Vaapi).ToArray();
        var index = Array.IndexOf(args, "-vf");
        Assert.Equal(SizeCaps.VideoFilter(SizeCap.Hd) + ",format=nv12,hwupload", args[index + 1]);
    }

    [Fact]
    public void Convert_Amf_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.Amf, 50, SizeCap.None);
        Assert.Equal(ReadGolden("amf-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_Amf_MatchesGolden()
    {
        Assert.Equal(ReadGolden("probe-amf.txt"), FfmpegArgv.Probe(EncoderKind.Amf).ToArray());
    }

    [Fact]
    public void Convert_VideoToolbox_Q50_MatchesGolden()
    {
        var args = FfmpegArgv.Convert("SRC", "PARTIAL", EncoderKind.VideoToolbox, 50, SizeCap.None);
        Assert.Equal(ReadGolden("videotoolbox-q50.txt"), args.ToArray());
    }

    [Fact]
    public void Probe_VideoToolbox_MatchesGolden()
    {
        Assert.Equal(ReadGolden("probe-videotoolbox.txt"), FfmpegArgv.Probe(EncoderKind.VideoToolbox).ToArray());
    }
}
