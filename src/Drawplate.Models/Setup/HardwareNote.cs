using System;

namespace Drawplate.Models.Setup;

public static class HardwareNote
{
    public static string? For(
        bool hasGpu,
        bool anyHardwareEncoderPassed,
        bool nvencDriverTooOld,
        string? minimumDriver)
    {
        if (!hasGpu)
        {
            return null;
        }

        if (anyHardwareEncoderPassed)
        {
            return null;
        }

        if (nvencDriverTooOld)
        {
            return minimumDriver is { } m
                ? $"Note: your NVIDIA driver is older than this FFmpeg's NVENC needs (minimum {m}). Update the driver to enable GPU encoding; Drawplate is using the CPU."
                : "Note: your NVIDIA driver is older than this FFmpeg's NVENC needs. Update the driver to enable GPU encoding; Drawplate is using the CPU.";
        }

        return "Note: no GPU encoder is available, so Drawplate is using the CPU. To enable GPU encoding, install the driver from your GPU vendor.";
    }
}
