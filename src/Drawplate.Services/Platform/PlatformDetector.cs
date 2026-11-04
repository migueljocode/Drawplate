using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

using Drawplate.Models.Encoding;

namespace Drawplate.Services.Platform;

public sealed record PlatformInfo(
    OperatingSystemKind Os,
    string Architecture,
    ImmutableHashSet<string> Tools,
    string? LinuxId,
    string? LinuxIdLike,
    int? FedoraRelease);

public sealed class PlatformDetector
{
    public static PlatformInfo Detect()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OperatingSystemKind.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? OperatingSystemKind.Linux
                : OperatingSystemKind.MacOS;

        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        var tools = DetectTools();
        var (linuxId, linuxIdLike, fedoraRelease) = DetectLinuxInfo();

        return new PlatformInfo(os, arch, tools, linuxId, linuxIdLike, fedoraRelease);
    }

    private static ImmutableHashSet<string> DetectTools()
    {
        var tools = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
        var toolNames = new[] { "winget", "scoop", "choco", "brew", "apt-get", "dnf", "pacman", "zypper" };
        foreach (var tool in toolNames)
        {
            if (ToolExists(tool))
            {
                tools = tools.Add(tool);
            }
        }
        return tools;
    }

    private static bool ToolExists(string tool)
    {
        try
        {
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tool,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is null)
            {
                return false;
            }
            proc.WaitForExit(2000);
            return proc.ExitCode == 0;
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (IOException)
        {
        }
        return false;
    }

    private static (string? LinuxId, string? LinuxIdLike, int? FedoraRelease) DetectLinuxInfo()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return (null, null, null);
        }

        try
        {
            if (!System.IO.File.Exists("/etc/os-release"))
            {
                return (null, null, null);
            }

            string? id = null, idLike = null;
            int? fedoraRelease = null;
            foreach (var line in System.IO.File.ReadAllLines("/etc/os-release"))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }
                var key = parts[0].Trim();
                var val = parts[1].Trim().Trim('"');
                if (key == "ID") id = val;
                else if (key == "ID_LIKE") idLike = val;
                else if (key == "VERSION_ID" && int.TryParse(val, out var ver)) fedoraRelease = ver;
            }
            return (id, idLike, fedoraRelease);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return (null, null, null);
    }
}
