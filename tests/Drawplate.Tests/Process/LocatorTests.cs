using System;
using System.IO;

using Drawplate.Models.Encoding;
using Drawplate.Services.Ffmpeg;

using Xunit;

namespace Drawplate.Tests.Process;

public class LocatorTests
{
    private static readonly string Exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    [Fact]
    public void Argument_Wins_OverEnvironment_OverPath()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: "/custom/ffmpeg",
            envVar: "/env/ffmpeg",
            pathFolders: ["/usr/bin"],
            fileExists: _ => true,
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.NotNull(candidate);
        Assert.Equal("/custom/ffmpeg", candidate!.Path);
        Assert.Equal(FfmpegSource.Argument, candidate.Source);
    }

    [Fact]
    public void Environment_Unusable_DoesNotFallThrough()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: "/bad/ffmpeg",
            pathFolders: ["/usr/bin"],
            fileExists: p => p == Path.Combine("/usr/bin", Exe),
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.NotNull(candidate);
        Assert.Equal("/bad/ffmpeg", candidate!.Path);
        Assert.Equal(FfmpegSource.Environment, candidate.Source);
    }

    [Fact]
    public void Locator_ArgumentMissing_ReturnsCandidate()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: "/nope/ffmpeg",
            envVar: null,
            pathFolders: ["/usr/bin"],
            fileExists: _ => false,
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.NotNull(candidate);
        Assert.Equal("/nope/ffmpeg", candidate!.Path);
        Assert.Equal(FfmpegSource.Argument, candidate.Source);
    }

    [Fact]
    public void Path_Found()
    {
        var expected = Path.Combine("/usr/bin", Exe);
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: ["/usr/bin", "/opt/bin"],
            fileExists: p => p == expected,
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.NotNull(candidate);
        Assert.Equal(expected, candidate!.Path);
        Assert.Equal(FfmpegSource.Path, candidate.Source);
    }

    [Fact]
    public void Locator_EmptyPathEntry_NotResolvedAgainstCwd()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: [string.Empty],
            fileExists: p => p == Exe,
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.Null(candidate);
    }

    [Fact]
    public void Locator_RelativePathEntry_Ignored()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: ["bin"],
            fileExists: p => p == Path.Combine("bin", Exe),
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.Null(candidate);
    }

    [Fact]
    public void KnownFolder_WinGetLinks_FoundWhenNotOnPath()
    {
        var links = Path.Combine("C:", "Users", "me", "AppData", "Local", "Microsoft", "WinGet", "Links");
        var expected = Path.Combine(links, "ffmpeg.exe");
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: [],
            fileExists: p => p == expected,
            directoryEnumerator: _ => [],
            knownFolders: [links],
            os: OperatingSystemKind.Windows);
        Assert.NotNull(candidate);
        Assert.Equal(expected, candidate!.Path);
        Assert.Equal(FfmpegSource.KnownFolder, candidate.Source);
    }

    [Fact]
    public void NothingFound_ReturnsNull()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: ["/usr/bin"],
            fileExists: _ => false,
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.Null(candidate);
    }

    [Fact]
    public void Windows_Glob_FindsGyanFfmpeg()
    {
        var packages = Path.Combine("C:", "Users", "me", "AppData", "Local", "Microsoft", "WinGet", "Packages");
        var gyan = Path.Combine(packages, "Gyan.FFmpeg_9.0.2");
        var build = Path.Combine(gyan, "ffmpeg-9.0.2-full_build");
        var expected = Path.Combine(build, "bin", "ffmpeg.exe");
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: [],
            fileExists: p => p == expected,
            directoryEnumerator: folder => folder == packages ? [gyan] : folder == gyan ? [build] : [],
            knownFolders: [packages],
            os: OperatingSystemKind.Windows);
        Assert.NotNull(candidate);
        Assert.Equal(expected, candidate!.Path);
        Assert.Equal(FfmpegSource.KnownFolder, candidate.Source);
    }

    [Fact]
    public void EnumerateDirectories_MissingFolder_ReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(FfmpegLocator.EnumerateDirectories(missing));
    }
}
