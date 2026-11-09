using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;

namespace Drawplate.Services.Platform;

/// <summary>Finds the operating system and the package managers. It starts no process (R07).</summary>
public sealed class PlatformDetector
{
    private static readonly string[] ToolNames = ["winget", "scoop", "choco", "brew", "apt-get", "dnf", "pacman", "zypper"];
    private static readonly string[] WindowsExtensions = [".exe", ".cmd", ".bat"];

    public static OperatingSystemKind DetectOs() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OperatingSystemKind.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? OperatingSystemKind.Linux
                : OperatingSystemKind.MacOS;

    public static PlatformInfo Detect() =>
        Detect(PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists);

    /// <summary>A package manager counts as present when its executable is in a PATH folder.</summary>
    public static PlatformInfo Detect(IEnumerable<string> pathFolders, Func<string, bool> fileExists, OperatingSystemKind? os = null)
    {
        ArgumentNullException.ThrowIfNull(pathFolders);
        ArgumentNullException.ThrowIfNull(fileExists);

        var kind = os ?? DetectOs();
        var folders = ImmutableArray.CreateRange(pathFolders);
        var tools = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in ToolNames)
        {
            if (ToolExists(tool, folders, fileExists, kind))
            {
                tools = tools.Add(tool);
            }
        }

        var (linuxId, linuxIdLike, fedoraRelease) = DetectLinuxInfo();
        return new PlatformInfo(kind, RuntimeInformation.ProcessArchitecture.ToString(), tools, linuxId, linuxIdLike, fedoraRelease);
    }

    private static bool ToolExists(string tool, ImmutableArray<string> folders, Func<string, bool> fileExists, OperatingSystemKind os)
    {
        var names = os == OperatingSystemKind.Windows
            ? [tool, .. Array.ConvertAll(WindowsExtensions, extension => tool + extension)]
            : new[] { tool };

        foreach (var folder in folders)
        {
            foreach (var name in names)
            {
                if (fileExists(Path.Combine(folder, name)))
                {
                    return true;
                }
            }
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
