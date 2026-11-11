using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Drawplate.Tests.Integration;

using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T3")]
[Collection("FakeExe")]
public sealed class CliTests
{
    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public async Task Cli_Help_ExitsZero_NoEnvironment(string option)
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, [option], root,
                new Dictionary<string, string?> { ["PATH"] = "", ["DRAWPLATE_FFMPEG"] = "/nonexistent" }).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Usage:", result.Stdout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Unhandled exception", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("-V")]
    [InlineData("--version")]
    public async Task Cli_Version_ExitsZero_NoEnvironment(string option)
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, [option], root,
                new Dictionary<string, string?> { ["PATH"] = "", ["DRAWPLATE_FFMPEG"] = "/nonexistent" }).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.StartsWith("drawplate ", result.Stdout, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_UnknownOption_ExitsTwo()
    {
        var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, ["--bogus"], Path.GetTempPath()).ConfigureAwait(true);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown option", result.Stderr, StringComparison.Ordinal);
        Assert.Empty(result.Stdout);
    }
    [Fact]
    public async Task Cli_MissingFfmpeg_ExitsThree_NoStackTrace()
    {
        var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, ["-l"], Path.GetTempPath(),
            new Dictionary<string, string?> { ["PATH"] = "", ["DRAWPLATE_FFMPEG"] = "/missing/ffmpeg" }).ConfigureAwait(true);
        Assert.Equal(3, result.ExitCode);
        Assert.DoesNotContain("Unhandled exception", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Cli_FfmpegPathMissing_ExitsThree()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ffmpeg");
        var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, ["--ffmpeg-path", missing, "-l"], Path.GetTempPath()).ConfigureAwait(true);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("file not found", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Cli_EmptyFolder_ExitsOne()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await MediaFixtures.Run(MediaFixtures.DrawplatePath, [], root).ConfigureAwait(true);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No supported video files", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    public async Task Cli_FolderConversion_ExitsZero()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "folder.mp4").ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(root, "compressed", "folder.mp4")));
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    public async Task Cli_OnlySoftware_NoHardwareNote()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "cpu.mp4").ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root, "-c").ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("[CPU]", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("Hardware", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_FfmpegTooOld_ExitsThree()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await RunFake(root, ["-l"],
                ["{\"if\":\"-version\",\"stdout\":\"ffmpeg version 5.1.1\\n\"}", "{\"exit\":0}"]).ConfigureAwait(true);
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("older than the supported", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_HardwareEncoderUnavailable_ExitsOne()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var encoder = OperatingSystem.IsMacOS() ? "videotoolbox" : "nvenc";
            var codec = OperatingSystem.IsMacOS() ? "h264_videotoolbox" : "h264_nvenc";
            var input = Path.Combine(root, "input.mp4");
            await File.WriteAllTextAsync(input, "fixture").ConfigureAwait(true);
            var result = await RunFake(root, ["-e", encoder, input],
                [
                    "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
                    "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n V..... " + codec + "\\n\"}",
                    "{\"if\":\"" + codec + "\",\"stderr\":\"hardware probe failed\",\"exit\":1}",
                    "{\"if\":\"-f lavfi\",\"exit\":0}"
                ]).ConfigureAwait(true);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains(encoder, result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_VaapiNoDevice_FallsBackToCpu()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await RunFake(root, ["-l"],
                [
                    "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
                    "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n V..... h264_vaapi\\n\"}",
                    "{\"if\":\"-f lavfi\",\"exit\":0}"
                ]).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("cpu", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("vaapi", result.Stdout, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_NoLibx264_NoHardware_ExitsThree()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await RunFake(root, ["-l"],
                [
                    "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
                    "{\"if\":\"-encoders\",\"stdout\":\"\"}",
                    "{\"exit\":0}"
                ]).ConfigureAwait(true);
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("This FFmpeg build has no libx264 encoder", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_ListEncoders_NoInput_ExitsZero()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var result = await RunFake(root, ["-l"],
                [
                    "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
                    "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}",
                    "{\"if\":\"-f lavfi\",\"exit\":0}"
                ]).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("cpu", result.Stdout, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_Help_StartsNoProcess()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var log = Path.Combine(root, "fake.log");
            var result = await RunFake(root, ["--help"], ["{\"exit\":0}"], log).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.False(File.Exists(log));
        }
        finally { Directory.Delete(root, true); }
    }


    private static async Task<MediaFixtures.Command> RunFake(string root, string[] arguments, string[] script, string? log = null)
    {
        var scriptPath = FakeExeHarness.WriteScript(script);
        var wrapper = FakeExeHarness.CreateWrapper();
        var logPath = log ?? Path.Combine(root, "fake.log");
        var args = new List<string> { "--ffmpeg-path", wrapper };
        args.AddRange(arguments);
        try
        {
            return await MediaFixtures.Run(MediaFixtures.DrawplatePath, args, root,
                new Dictionary<string, string?> { ["FAKE_EXE_SCRIPT"] = scriptPath, ["FAKE_EXE_LOG"] = logPath }).ConfigureAwait(true);
        }
        finally
        {
            File.Delete(scriptPath);
            File.Delete(wrapper);
        }
    }

}
