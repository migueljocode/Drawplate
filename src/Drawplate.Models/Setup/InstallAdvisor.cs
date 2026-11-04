using System;
using System.Collections.Immutable;
using System.Linq;

using Drawplate.Models.Encoding;

namespace Drawplate.Models.Setup;

public static class InstallAdvisor
{
    public static ImmutableArray<InstallSuggestion> Suggestions(PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return platform.Os switch
        {
            OperatingSystemKind.Windows => WindowsSuggestions(platform),
            OperatingSystemKind.MacOS => MacOSSuggestions(platform),
            OperatingSystemKind.Linux => LinuxSuggestions(platform),
            _ => [],
        };
    }

    private static ImmutableArray<InstallSuggestion> WindowsSuggestions(PlatformInfo platform)
    {
        var builder = ImmutableArray.CreateBuilder<InstallSuggestion>();

        var wingetArgs = ImmutableArray.Create<string>(
            "install", "--id", "Gyan.FFmpeg", "-e",
            "--accept-package-agreements", "--accept-source-agreements");

        if (platform.Tools.Contains("winget"))
        {
            builder.Add(new InstallSuggestion(
                "winget",
                ["Install it with winget (no administrator rights needed):",
                 "  winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements"],
                "winget", wingetArgs, true));
        }

        if (platform.Tools.Contains("scoop"))
        {
            builder.Add(new InstallSuggestion(
                "scoop",
                ["Install it with scoop:",
                 "  scoop install ffmpeg"],
                "scoop", ImmutableArray.Create("install", "ffmpeg"), true));
        }

        if (platform.Tools.Contains("choco"))
        {
            builder.Add(new InstallSuggestion(
                "chocolatey",
                ["Install it with chocolatey (needs Administrator):",
                 "  choco install ffmpeg"],
                "choco", ImmutableArray.Create("install", "ffmpeg"), false));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<InstallSuggestion> MacOSSuggestions(PlatformInfo platform)
    {
        var builder = ImmutableArray.CreateBuilder<InstallSuggestion>();

        if (platform.Tools.Contains("brew"))
        {
            builder.Add(new InstallSuggestion(
                "homebrew",
                ["Install it with Homebrew:",
                 "  brew install ffmpeg"],
                "brew", ImmutableArray.Create("install", "ffmpeg"), true));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<InstallSuggestion> LinuxSuggestions(PlatformInfo platform)
    {
        var builder = ImmutableArray.CreateBuilder<InstallSuggestion>();

        // apt
        if (platform.Tools.Contains("apt-get") ||
            string.Equals(platform.LinuxId, "ubuntu", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platform.LinuxId, "debian", StringComparison.OrdinalIgnoreCase))
        {
            builder.Add(new InstallSuggestion(
                "apt",
                ["Install it with your package manager (administrator rights are required, so run this yourself):",
                 "  sudo apt install ffmpeg"],
                null, [], false));
        }

        // pacman
        if (platform.Tools.Contains("pacman") ||
            string.Equals(platform.LinuxId, "arch", StringComparison.OrdinalIgnoreCase) ||
            (platform.LinuxIdLike is not null && platform.LinuxIdLike.Contains("arch", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Add(new InstallSuggestion(
                "pacman",
                ["Install it with your package manager (administrator rights are required, so run this yourself):",
                 "  sudo pacman -S ffmpeg"],
                null, [], false));
        }

        // dnf / Fedora
        if (platform.Tools.Contains("dnf") ||
            string.Equals(platform.LinuxId, "fedora", StringComparison.OrdinalIgnoreCase))
        {
            var release = platform.FedoraRelease is { } r
                ? $"https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-{r}.noarch.rpm"
                : "https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm";
            builder.Add(new InstallSuggestion(
                "dnf",
                ["Install it with your package manager (administrator rights are required, so run this yourself):",
                 "Fedora needs RPM Fusion for libx264:",
                 $"  sudo dnf install {release}",
                 "  sudo dnf swap ffmpeg-free ffmpeg --allowerasing"],
                null, [], false));
        }

        // zypper / openSUSE
        if (platform.Tools.Contains("zypper") ||
            string.Equals(platform.LinuxId, "opensuse-leap", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platform.LinuxId, "opensuse-tumbleweed", StringComparison.OrdinalIgnoreCase) ||
            (platform.LinuxIdLike is not null && platform.LinuxIdLike.Contains("suse", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Add(new InstallSuggestion(
                "zypper",
                ["Install it with your package manager (administrator rights are required, so run this yourself):",
                 "  sudo zypper install ffmpeg",
                 "  (openSUSE: use the Packman repository for libx264)"],
                null, [], false));
        }

        return builder.ToImmutable();
    }

    public static string NotFoundText(PlatformInfo platform, ImmutableArray<InstallSuggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return platform.Os switch
        {
            OperatingSystemKind.Windows => WindowsNotFoundText(suggestions),
            OperatingSystemKind.MacOS => MacOsNotFoundText(suggestions),
            OperatingSystemKind.Linux => LinuxNotFoundText(suggestions),
            _ => "FFmpeg was not found.",
        };
    }

    private static string WindowsNotFoundText(ImmutableArray<InstallSuggestion> suggestions)
    {
        var hasWinget = suggestions.Any(s => s.Executable == "winget");
        if (hasWinget)
        {
            return
                "FFmpeg was not found.\n" +
                "\n" +
                "Install it with winget (no administrator rights needed):\n" +
                "  winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements\n" +
                "\n" +
                "Then open a new terminal and run Drawplate again.\n" +
                "Already have FFmpeg somewhere? Use --ffmpeg-path <file> or set DRAWPLATE_FFMPEG.\n" +
                "Problems? See \"Troubleshooting FFmpeg\" in the README.";
        }

        return
            "FFmpeg was not found.\n" +
            "\n" +
            "winget was not found.\n" +
            "Hint: It ships with Windows 10 (1809 or newer) and Windows 11 as \"App Installer\". Install it from the Microsoft Store, or install FFmpeg manually (see \"Troubleshooting FFmpeg\" in the README).";
    }

    private static string MacOsNotFoundText(ImmutableArray<InstallSuggestion> suggestions)
    {
        var hasBrew = suggestions.Any(s => s.Executable == "brew");
        if (hasBrew)
        {
            return
                "FFmpeg was not found.\n" +
                "\n" +
                "Install it with Homebrew:\n" +
                "  brew install ffmpeg\n" +
                "\n" +
                "Then open a new terminal and run Drawplate again.\n" +
                "Already have FFmpeg somewhere? Use --ffmpeg-path <file> or set DRAWPLATE_FFMPEG.\n" +
                "Problems? See \"Troubleshooting FFmpeg\" in the README.";
        }

        return
            "FFmpeg was not found.\n" +
            "\n" +
            "Homebrew was not found.\n" +
            "Hint: Install it from https://brew.sh, then run: brew install ffmpeg";
    }

    private static string LinuxNotFoundText(ImmutableArray<InstallSuggestion> suggestions)
    {
        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add("FFmpeg was not found.");
        lines.Add("");
        lines.Add("Install it with your package manager (administrator rights are required, so run this yourself):");

        foreach (var suggestion in suggestions)
        {
            foreach (var line in suggestion.DisplayLines)
            {
                lines.Add(line);
            }
        }

        return string.Join("\n", lines);
    }
}
