using Drawplate.Models.Failures;

using Xunit;

namespace Drawplate.Tests.Unit;

public class FailureClassifierTests
{
    [Fact]
    public void Classify_InvalidData()
    {
        Assert.Equal(FailureKind.InvalidInput,
            FailureClassifier.Classify("Invalid data found when processing input"));
    }

    [Fact]
    public void Classify_NvencTooOld_ParsesVersions()
    {
        var text = "Driver does not support the required nvenc API version. Required: 11.1 Found: 10.1\nThe minimum required Nvidia driver for nvenc is 450.80 or newer";
        var result = FailureClassifier.ParseNvenc(text);
        Assert.Equal("11.1", result.Required);
        Assert.Equal("10.1", result.Found);
        Assert.Equal("450.80", result.MinimumDriver);
    }

    [Fact]
    public void Classify_UnknownEncoder()
    {
        Assert.Equal(FailureKind.EncoderUnavailable,
            FailureClassifier.Classify("Unknown encoder 'h264_nvenc'"));
    }

    [Fact]
    public void Classify_Other_IsUnknown()
    {
        Assert.Equal(FailureKind.Unknown,
            FailureClassifier.Classify("some random error"));
    }

    [Fact]
    public void ShortReason_UsesLastLine()
    {
        var reason = FailureClassifier.ShortReason(FailureKind.EncoderUnavailable, "line one\nline two\n");
        Assert.Equal("encoder failed: line two", reason);
    }

    [Fact]
    public void Classify_DeviceAccess()
    {
        Assert.Equal(FailureKind.DeviceAccess,
            FailureClassifier.Classify("Permission denied: /dev/dri/renderD128"));
    }
}
