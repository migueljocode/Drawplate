using Drawplate.Models.Setup;

using Xunit;

namespace Drawplate.Tests.Unit;

public class HardwareNoteTests
{
    [Fact]
    public void Note_GpuAndNoHwEncoder_Gpu01()
    {
        var note = HardwareNote.For(hasGpu: true, anyHardwareEncoderPassed: false, nvencDriverTooOld: false, minimumDriver: null);
        Assert.Equal("Note: no GPU encoder is available, so Drawplate is using the CPU. To enable GPU encoding, install the driver from your GPU vendor.", note);
    }

    [Fact]
    public void Note_NvencTooOld_Gpu02WithMinimum()
    {
        var note = HardwareNote.For(hasGpu: true, anyHardwareEncoderPassed: false, nvencDriverTooOld: true, minimumDriver: "450.80");
        Assert.Equal("Note: your NVIDIA driver is older than this FFmpeg's NVENC needs (minimum 450.80). Update the driver to enable GPU encoding; Drawplate is using the CPU.", note);
    }

    [Fact]
    public void Note_NoGpu_Null()
    {
        var note = HardwareNote.For(hasGpu: false, anyHardwareEncoderPassed: false, nvencDriverTooOld: false, minimumDriver: null);
        Assert.Null(note);
    }

    [Fact]
    public void Note_HwEncoderWorks_Null()
    {
        var note = HardwareNote.For(hasGpu: true, anyHardwareEncoderPassed: true, nvencDriverTooOld: false, minimumDriver: null);
        Assert.Null(note);
    }

    [Fact]
    public void Note_NvencTooOld_NoMinimum_Gpu02()
    {
        var note = HardwareNote.For(hasGpu: true, anyHardwareEncoderPassed: false, nvencDriverTooOld: true, minimumDriver: null);
        Assert.Equal("Note: your NVIDIA driver is older than this FFmpeg's NVENC needs. Update the driver to enable GPU encoding; Drawplate is using the CPU.", note);
    }
}
