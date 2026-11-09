using System;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Platform;

using Xunit;

namespace Drawplate.Tests.Process;

public class PlatformTests
{
    private static readonly string Sep = Path.PathSeparator.ToString();

    [Fact]
    public void Platform_Detect_StartsNoProcess()
    {
        // The detector only asks whether files exist; a fake file system is enough to find a package manager.
        var brew = Path.Combine("/opt/homebrew/bin", "brew");
        var info = PlatformDetector.Detect(["/opt/homebrew/bin"], p => p == brew, OperatingSystemKind.MacOS);
        Assert.Contains("brew", info.Tools);
        Assert.DoesNotContain("dnf", info.Tools);
    }

    [Fact]
    public void Platform_ToolOnPath_WindowsExtensionsTried()
    {
        var folder = Path.Combine("C:", "Tools");
        var info = PlatformDetector.Detect(
            [folder],
            p => p == Path.Combine(folder, "winget.exe") || p == Path.Combine(folder, "scoop.cmd"),
            OperatingSystemKind.Windows);
        Assert.Contains("winget", info.Tools);
        Assert.Contains("scoop", info.Tools);
    }

    [Fact]
    public void Platform_ToolOnPath_UnixExtensionsNotTried()
    {
        var folder = "/usr/bin";
        var info = PlatformDetector.Detect([folder], p => p == Path.Combine(folder, "brew.exe"), OperatingSystemKind.Linux);
        Assert.DoesNotContain("brew", info.Tools);
    }

    [Fact]
    public void PathFolders_DropsEmptyAndRelativeEntries()
    {
        var parsed = PathFolders.Parse(string.Join(Sep, string.Empty, "/usr/bin", "rel/bin", " /opt/bin ", string.Empty));
        Assert.Equal<string[]>(["/usr/bin", "/opt/bin"], parsed.ToArray());
    }

    [Fact]
    public void PathFolders_Null_ReturnsEmpty()
    {
        Assert.Empty(PathFolders.Parse(null));
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOS)]
    public void KnownFolders_Linux_Order(OperatingSystemKind os)
    {
        Assert.Equal<string[]>(
            ["/opt/homebrew/bin", "/usr/local/bin", "/home/linuxbrew/.linuxbrew/bin", "/usr/bin"],
            KnownFolders.For(os, _ => null).ToArray());
    }

    [Fact]
    public void KnownFolders_Windows_UsesLocalAppData()
    {
        var local = Path.Combine("C:", "Users", "me", "AppData", "Local");
        var folders = KnownFolders.For(
            OperatingSystemKind.Windows,
            name => name switch
            {
                "LOCALAPPDATA" => local,
                "USERPROFILE" => Path.Combine("C:", "Users", "me"),
                _ => null,
            });
        Assert.Equal(Path.Combine(local, "Microsoft", "WinGet", "Links"), folders[0]);
        Assert.Equal(Path.Combine(local, "Microsoft", "WinGet", "Packages"), folders[1]);
        Assert.Contains(Path.Combine("C:", "Users", "me", "scoop", "shims"), folders);
        Assert.Contains(Path.Combine(@"C:\ProgramData", "chocolatey", "bin"), folders);
    }

    [Fact]
    public void DetectOs_MatchesRuntime()
    {
        var expected = OperatingSystem.IsWindows() ? OperatingSystemKind.Windows
            : OperatingSystem.IsLinux() ? OperatingSystemKind.Linux
            : OperatingSystemKind.MacOS;
        Assert.Equal(expected, PlatformDetector.DetectOs());
    }
}
