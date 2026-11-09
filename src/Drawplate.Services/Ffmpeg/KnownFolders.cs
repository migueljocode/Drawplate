using System;
using System.Collections.Immutable;
using System.IO;

using Drawplate.Models.Encoding;

namespace Drawplate.Services.Ffmpeg;

/// <summary>Where FFmpeg is looked for after PATH (D.5), per operating system.</summary>
public static class KnownFolders
{
    public static ImmutableArray<string> For(OperatingSystemKind os, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(getEnv);
        return os == OperatingSystemKind.Windows
            ? ForWindows(getEnv)
            : ["/opt/homebrew/bin", "/usr/local/bin", "/home/linuxbrew/.linuxbrew/bin", "/usr/bin"];
    }

    private static ImmutableArray<string> ForWindows(Func<string, string?> getEnv)
    {
        var folders = ImmutableArray.CreateBuilder<string>();

        if (getEnv("LOCALAPPDATA") is { Length: > 0 } localAppData)
        {
            folders.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links"));
            // The locator looks inside this folder for Gyan.FFmpeg_*\ffmpeg-*\bin.
            folders.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Packages"));
        }

        if (getEnv("USERPROFILE") is { Length: > 0 } userProfile)
        {
            folders.Add(Path.Combine(userProfile, "scoop", "shims"));
        }

        var programData = getEnv("ProgramData") is { Length: > 0 } value ? value : @"C:\ProgramData";
        folders.Add(Path.Combine(programData, "chocolatey", "bin"));

        return folders.ToImmutable();
    }
}
