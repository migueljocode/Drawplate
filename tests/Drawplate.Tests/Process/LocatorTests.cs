using System.Runtime.InteropServices;

using Drawplate.Services.Ffmpeg;

using Xunit;

namespace Drawplate.Tests.Process;

public class LocatorTests
{
    [Fact]
    public void Argument_Wins_OverEnvironment_OverPath()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: "/custom/ffmpeg",
            envVar: "/env/ffmpeg",
            pathFolders: ["/usr/bin"],
            fileExists: p => p == "/custom/ffmpeg" || p == "/env/ffmpeg" || p == "/usr/bin/ffmpeg",
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
            fileExists: p => p == "/usr/bin/ffmpeg",
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.Null(candidate);
    }

    [Fact]
    public void Path_Found()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: ["/usr/bin", "/opt/bin"],
            fileExists: p => p == "/usr/bin/ffmpeg",
            directoryEnumerator: _ => [],
            knownFolders: []);
        Assert.NotNull(candidate);
        Assert.Equal("/usr/bin/ffmpeg", candidate!.Path);
        Assert.Equal(FfmpegSource.Path, candidate.Source);
    }

    [Fact(Skip = "Windows-only known folder test")]
    public void KnownFolder_WinGetLinks_FoundWhenNotOnPath()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: [],
            fileExists: p => p == "C:\\\\Users\\\\me\\\\AppData\\\\Local\\\\Microsoft\\\\WinGet\\\\Links\\\\ffmpeg.exe",
            directoryEnumerator: _ => [],
            knownFolders: ["C:\\\\Users\\\\me\\\\AppData\\\\Local\\\\Microsoft\\\\WinGet\\\\Links"]);
        Assert.NotNull(candidate);
        Assert.Equal(FfmpegSource.KnownFolder, candidate!.Source);
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

    [Fact(Skip = "Windows-only glob test")]
    public void Windows_Glob_FindsGyanFfmpeg()
    {
        var candidate = FfmpegLocator.Locate(
            ffmpegPath: null,
            envVar: null,
            pathFolders: [],
            fileExists: p => p == "C:\\\\Gyan.FFmpeg_9.0.2\\\\ffmpeg-9.0.2\\\\bin\\\\ffmpeg.exe",
            directoryEnumerator: folder =>
            {
                if (folder == "C:\\\\Users\\\\me\\\\AppData\\\\Local\\\\Microsoft\\\\WinGet\\\\Packages")
                    return ["C:\\\\Gyan.FFmpeg_9.0.2\\\\ffmpeg-9.0.2\\\\bin"];
                return [];
            },
            knownFolders: ["C:\\\\Users\\\\me\\\\AppData\\\\Local\\\\Microsoft\\\\WinGet\\\\Packages"]);
        Assert.NotNull(candidate);
        Assert.Equal("C:\\\\Gyan.FFmpeg_9.0.2\\\\ffmpeg-9.0.2\\\\bin\\\\ffmpeg.exe", candidate!.Path);
    }
}
