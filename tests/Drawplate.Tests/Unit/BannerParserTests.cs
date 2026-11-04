using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Drawplate.Models.Ffmpeg;

using Xunit;

namespace Drawplate.Tests.Unit;

public class BannerParserTests
{
    [Fact]
    public void ParseVersion_6_1_1_Ubuntu()
    {
        var v = FfmpegBannerParser.ParseVersion("ffmpeg version 6.1.1-3ubuntu5 Copyright (c) 2000-2024 the FFmpeg developers");
        Assert.False(v.IsMaster);
        Assert.Equal(new Version(6, 1, 1), v.Version);
    }

    [Fact]
    public void ParseVersion_GyanBuild()
    {
        var v = FfmpegBannerParser.ParseVersion("ffmpeg version 9.0.2-full_build-www.gyan.dev Copyright (c) 2000-2024 the FFmpeg developers");
        Assert.False(v.IsMaster);
        Assert.Equal(new Version(9, 0, 2), v.Version);
    }

    [Fact]
    public void ParseVersion_MasterBuild()
    {
        var v = FfmpegBannerParser.ParseVersion("ffmpeg version N-128212-g17a4a474f4 Copyright (c) 2000-2024 the FFmpeg developers");
        Assert.True(v.IsMaster);
        Assert.Equal(new Version(99, 0), v.Version);
    }

    [Fact]
    public void ParseEncoders_ExtractsNames()
    {
        var output = "ffmpeg version 6.1.1\n  V..... libx264\n  V..... h264_nvenc\n   A..... aac\n";
        var encoders = FfmpegBannerParser.ParseEncoders(output);
        Assert.Contains("libx264", encoders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("h264_nvenc", encoders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("aac", encoders, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDuration_Valid()
    {
        var duration = FfmpegBannerParser.ParseDuration("Duration: 00:01:05.50, start: 0.000000");
        Assert.Equal(TimeSpan.FromSeconds(65.5), duration);
    }

    [Fact]
    public void ParseDuration_NA_IsNull()
    {
        var duration = FfmpegBannerParser.ParseDuration("Duration: N/A, start: 0.000000");
        Assert.Null(duration);
    }

    [Fact]
    public void HasVideoStream_VideoPresent()
    {
        var stderr = "Stream #0:0(und): Video: h264, yuv420p, 1280x720";
        Assert.True(FfmpegBannerParser.HasVideoStream(stderr));
    }

    [Fact]
    public void HasVideoStream_AudioOnly_False()
    {
        var stderr = "Stream #0:0(und): Audio: aac, 48000 Hz";
        Assert.False(FfmpegBannerParser.HasVideoStream(stderr));
    }
}
