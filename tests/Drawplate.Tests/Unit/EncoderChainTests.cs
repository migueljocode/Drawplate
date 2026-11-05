using System;
using System.Collections.Immutable;
using System.Linq;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class EncoderChainTests
{
    [Fact]
    public void Auto_Linux_AllWorking()
    {
        var working = ImmutableArray.Create(EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Cpu);
        var result = EncoderChain.Build("auto", OperatingSystemKind.Linux, working);
        Assert.Null(result.Error);
        Assert.Equal([EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Cpu], result.Chain.ToArray());
    }

    [Fact]
    public void Auto_OnlyCpu()
    {
        var working = ImmutableArray.Create(EncoderKind.Cpu);
        var result = EncoderChain.Build("auto", OperatingSystemKind.Linux, working);
        Assert.Null(result.Error);
        Assert.Equal([EncoderKind.Cpu], result.Chain.ToArray());
    }

    [Fact]
    public void Specific_Qsv_ThenCpu()
    {
        var working = ImmutableArray.Create(EncoderKind.Qsv, EncoderKind.Cpu);
        var result = EncoderChain.Build("qsv", OperatingSystemKind.Linux, working);
        Assert.Null(result.Error);
        Assert.Equal([EncoderKind.Qsv, EncoderKind.Cpu], result.Chain.ToArray());
    }

    [Fact]
    public void Specific_NotWorking_ReturnsE_RUN_02()
    {
        var working = ImmutableArray.Create(EncoderKind.Cpu);
        var result = EncoderChain.Build("nvenc", OperatingSystemKind.Linux, working);
        Assert.NotNull(result.Error);
        Assert.Equal("E-RUN-02", result.Error!.Id);
    }

    [Fact]
    public void Specific_NotWorking_ReturnsE_RUN_02_Message()
    {
        var working = ImmutableArray.Create(EncoderKind.Cpu);
        var result = EncoderChain.Build("nvenc", OperatingSystemKind.Linux, working);
        Assert.Equal("Encoder 'nvenc' is not working on this machine.", result.Error!.Text);
    }

    [Fact]
    public void UnknownEncoder_ReturnsE_USAGE_04()
    {
        var working = ImmutableArray.Create(EncoderKind.Cpu);
        var result = EncoderChain.Build("foo", OperatingSystemKind.Linux, working);
        Assert.NotNull(result.Error);
        Assert.Equal("E-USAGE-04", result.Error!.Id);
    }

    [Fact]
    public void WrongOs_AmfOnLinux_ReturnsE_USAGE_12()
    {
        var working = ImmutableArray.Create(EncoderKind.Cpu);
        var result = EncoderChain.Build("amf", OperatingSystemKind.Linux, working);
        Assert.NotNull(result.Error);
        Assert.Equal("E-USAGE-12", result.Error!.Id);
        Assert.Equal("Encoder 'amf' is not available on Linux.", result.Error!.Text);
    }

    [Fact]
    public void CpuOnly_NoLibx264_Empty()
    {
        var working = ImmutableArray<EncoderKind>.Empty;
        var result = EncoderChain.Build("auto", OperatingSystemKind.Linux, working);
        Assert.Null(result.Error);
        Assert.Empty(result.Chain);
    }
}
