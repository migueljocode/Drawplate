using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace Drawplate.Services.Platform;

public sealed record GpuResult(bool HasGpu, string? Vendor, string? Detail);

public sealed class GpuDetector
{
    public static bool HasGpu()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return DetectLinuxGpu();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return DetectWindowsGpu();
        }

        return true;
    }

    private static bool DetectLinuxGpu()
    {
        var vendors = new[] { "0x10de", "0x1002", "0x8086" };
        try
        {
            if (!Directory.Exists("/sys/class/drm"))
            {
                return false;
            }

            foreach (var cardDir in Directory.GetDirectories("/sys/class/drm"))
            {
                var deviceDir = Path.Combine(cardDir, "device");
                if (!Directory.Exists(deviceDir))
                {
                    continue;
                }

                var vendorFile = Path.Combine(deviceDir, "vendor");
                if (!File.Exists(vendorFile))
                {
                    continue;
                }

                var vendorContent = File.ReadAllText(vendorFile).Trim();
                if (Array.IndexOf(vendors, vendorContent) >= 0)
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return false;
    }

    [SupportedOSPlatform("Windows")]
    private static bool DetectWindowsGpu()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (key == null)
            {
                return false;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null)
                {
                    continue;
                }

                var matchingDeviceId = subKey.GetValue("MatchingDeviceId") as string;
                if (matchingDeviceId != null &&
                    (matchingDeviceId.Contains("VEN_10DE") ||
                     matchingDeviceId.Contains("VEN_1002") ||
                     matchingDeviceId.Contains("VEN_8086")) &&
                    !matchingDeviceId.Contains("VEN_1414"))
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return false;
    }
}
