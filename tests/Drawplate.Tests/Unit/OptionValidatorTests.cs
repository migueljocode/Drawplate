using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;

using Xunit;

namespace Drawplate.Tests.Unit;

public class OptionValidatorTests
{
    [Fact]
    public void E_USAGE_04_Text()
    {
        var msg = Messages.E_USAGE_04("foo", "cpu, nvenc");
        Assert.Equal("E-USAGE-04", msg.Id);
        Assert.Equal("Unknown encoder 'foo'.", msg.Text);
        Assert.Equal("Choose one of: cpu, nvenc.", msg.Hint);
        Assert.Equal(2, msg.ExitCode);
    }

    [Fact]
    public void E_USAGE_05_Text()
    {
        var msg = Messages.E_USAGE_05();
        Assert.Equal("Quality must be a number from 1 to 100 (higher = better).", msg.Text);
    }

    [Fact]
    public void E_USAGE_06_Text()
    {
        var msg = Messages.E_USAGE_06("8k");
        Assert.Equal("Invalid max size '8k'.", msg.Text);
        Assert.Equal("Choose one of: sd, hd, fullhd, 4k.", msg.Hint);
    }

    [Fact]
    public void E_USAGE_11_Text()
    {
        var msg = Messages.E_USAGE_11();
        Assert.Equal("Use either -i <folder> or file names, not both.", msg.Text);
    }

    [Fact]
    public void E_USAGE_12_Text()
    {
        var msg = Messages.E_USAGE_12("amf", "Linux", "nvenc, qsv, vaapi, cpu");
        Assert.Equal("Encoder 'amf' is not available on Linux.", msg.Text);
    }

    [Fact]
    public void Validate_Defaults_AreQuality50_AutoEncoder_NoCap()
    {
        var (options, error) = OptionValidator.Validate(new RawOptions(), OperatingSystemKind.Linux);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Null(options!.Encoder); // auto means unresolved
        Assert.Equal(50, options.Quality);
        Assert.Equal(SizeCap.None, options.MaxSize);
    }

    [Fact]
    public void Validate_CpuFlag_ForcesCpu()
    {
        var raw = new RawOptions { Encoder = "nvenc", Cpu = true };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(error);
        Assert.Equal(EncoderKind.Cpu, options!.Encoder);
    }

    [Fact]
    public void Validate_AutoEncoder_Accepted()
    {
        var raw = new RawOptions { Encoder = "auto" };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(error);
        Assert.Null(options!.Encoder);
    }

    [Fact]
    public void Validate_UnknownEncoder()
    {
        var raw = new RawOptions { Encoder = "foo" };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Equal("E-USAGE-04", error!.Id);
    }

    [Fact]
    public void Validate_WrongOs_AmfOnLinux()
    {
        var raw = new RawOptions { Encoder = "amf" };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Equal("E-USAGE-12", error!.Id);
    }

    [Fact]
    public void Validate_BadQuality()
    {
        var raw = new RawOptions { Quality = "200" };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Equal("E-USAGE-05", error!.Id);
    }

    [Fact]
    public void Validate_BadMaxSize()
    {
        var raw = new RawOptions { MaxSize = "8k" };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Equal("E-USAGE-06", error!.Id);
    }

    [Fact]
    public void Validate_InputFolderWithFiles()
    {
        var raw = new RawOptions { InputFolder = "/in", Files = ["a.mp4"] };
        var (options, error) = OptionValidator.Validate(raw, OperatingSystemKind.Linux);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Equal("E-USAGE-11", error!.Id);
    }
}
