using System;
using System.Linq;

using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;

using Xunit;

namespace Drawplate.Tests.Unit;

public class InstallAdvisorTests
{
    [Fact]
    public void Windows_WithWinget_FirstIsWingetRunnable()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Windows, "x64",
            ImmutableHashSet.Create(["winget"]), null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        Assert.Single(suggestions);
        Assert.Equal("winget", suggestions[0].Executable);
        Assert.True(suggestions[0].RunnableByApp);
        Assert.Equal(["install", "--id", "Gyan.FFmpeg", "-e", "--accept-package-agreements", "--accept-source-agreements"],
            suggestions[0].Arguments.ToArray());
    }

    [Fact]
    public void Windows_WithoutWinget_ShowsE_FF_05_Text()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Windows, "x64",
            [], null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        var text = InstallAdvisor.NotFoundText(platform, suggestions);
        Assert.Contains("winget was not found", text);
    }

    [Fact]
    public void Windows_WithScoop_AddsScoopRunnable()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Windows, "x64",
            ImmutableHashSet.Create(["winget", "scoop"]), null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        Assert.Equal(2, suggestions.Length);
        var scoop = suggestions[1];
        Assert.Equal("scoop", scoop.Executable);
        Assert.True(scoop.RunnableByApp);
    }

    [Fact]
    public void Windows_WithChoco_AddsChocoNotRunnable()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Windows, "x64",
            ImmutableHashSet.Create(["winget", "choco"]), null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        var choco = suggestions.Last(s => s.Executable == "choco");
        Assert.False(choco.RunnableByApp);
    }

    [Fact]
    public void MacOs_WithBrew_Runnable()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.MacOS, "arm64",
            ImmutableHashSet.Create(["brew"]), null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        Assert.Single(suggestions);
        Assert.Equal("brew", suggestions[0].Executable);
        Assert.True(suggestions[0].RunnableByApp);
    }

    [Fact]
    public void MacOs_WithoutBrew_ShowsE_FF_06()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.MacOS, "arm64",
            [], null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        var text = InstallAdvisor.NotFoundText(platform, suggestions);
        Assert.Contains("Homebrew was not found", text);
    }

    [Fact]
    public void Linux_Apt_PrintsSudoAptInstall_NotRunnable()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Linux, "x64",
            ImmutableHashSet.Create(["apt-get"]), "ubuntu", "debian", null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        Assert.Single(suggestions);
        Assert.False(suggestions[0].RunnableByApp);
        Assert.Contains("  sudo apt install ffmpeg", suggestions[0].DisplayLines);
    }

    [Fact]
    public void Linux_Fedora_PrintsRpmFusionTwoStep()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Linux, "x64",
            ImmutableHashSet.Create(["dnf"]), "fedora", null, 40);
        var suggestions = InstallAdvisor.Suggestions(platform);
        Assert.Single(suggestions);
        Assert.False(suggestions[0].RunnableByApp);
        Assert.Contains("rpmfusion-free-release-40", suggestions[0].DisplayLines[2]);
        Assert.Contains("sudo dnf swap ffmpeg-free ffmpeg --allowerasing", suggestions[0].DisplayLines[3]);
    }

    [Fact]
    public void NotFoundText_Windows_MatchesCatalogExactly()
    {
        var platform = new PlatformInfo(
            OperatingSystemKind.Windows, "x64",
            ImmutableHashSet.Create(["winget"]), null, null, null);
        var suggestions = InstallAdvisor.Suggestions(platform);
        var text = InstallAdvisor.NotFoundText(platform, suggestions);
        var expected =
            "FFmpeg was not found.\n" +
            "\n" +
            "Install it with winget (no administrator rights needed):\n" +
            "  winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements\n" +
            "\n" +
            "Then open a new terminal and run Drawplate again.\n" +
            "Already have FFmpeg somewhere? Use --ffmpeg-path <file> or set DRAWPLATE_FFMPEG.\n" +
            "Problems? See \"Troubleshooting FFmpeg\" in the README.";
        Assert.Equal(expected, text);
    }
}
