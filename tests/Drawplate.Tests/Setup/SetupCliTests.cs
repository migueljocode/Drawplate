using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Drawplate.Tests.Cli;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Setup;

[Collection("FakeExe")]
public sealed class SetupCliTests
{
    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_N_NoPrompt_Exit3()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-ffmpeg-" + Guid.NewGuid().ToString("N"));
        var result = await RunCliAsync(["setup", "-n", "--ffmpeg-path", missing]).ConfigureAwait(true);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("could not be run (file not found)", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Setup_N_NoFfmpeg_E_FF_01_Exit3()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "drawplate-setup-empty-" + Guid.NewGuid().ToString("N"));
        var localAppData = Path.Combine(folder, "local");
        var userProfile = Path.Combine(folder, "user");
        var programData = Path.Combine(folder, "program-data");
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(userProfile);
        Directory.CreateDirectory(programData);
        await File.WriteAllTextAsync(Path.Combine(folder, "winget.cmd"), "@echo off\r\nexit /b 0\r\n").ConfigureAwait(true);

        var environment = new Dictionary<string, string?>
        {
            ["PATH"] = folder,
            ["LOCALAPPDATA"] = localAppData,
            ["USERPROFILE"] = userProfile,
            ["ProgramData"] = programData,
            ["DRAWPLATE_FFMPEG"] = null
        };
        try
        {
            var result = await RunCliAsync(["setup", "-n"], environment).ConfigureAwait(true);

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("FFmpeg was not found.", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("winget install", result.Stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Setup_FfmpegPresent_Exit0()
    {
        var script = FakeExeHarness.WriteScript([
            "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
            "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}",
            "{\"exit\":0}"
        ]);
        var wrapper = FakeExeHarness.CreateWrapper();
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
        try
        {
            var result = await RunCliAsync(["setup", "--ffmpeg-path", wrapper]).ConfigureAwait(true);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("FFmpeg is already installed." + Environment.NewLine, result.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Convert_Missing_NonInteractive_Exit3()
    {
        var folder = Path.Combine(Path.GetTempPath(), "drawplate-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "clip.mp4");
        await File.WriteAllTextAsync(source, "fixture").ConfigureAwait(true);
        var missing = Path.Combine(Path.GetTempPath(), "missing-ffmpeg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await RunCliAsync(["--no-install", "--ffmpeg-path", missing, source]).ConfigureAwait(true);

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("could not be run (file not found)", result.Stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Y_FfmpegPresent_ExitsWithoutConverting()
    {
        var script = FakeExeHarness.WriteScript([
            "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
            "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}",
            "{\"exit\":0}"
        ]);
        var wrapper = FakeExeHarness.CreateWrapper();
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
        try
        {
            var result = await RunCliAsync(["-y", "--ffmpeg-path", wrapper]).ConfigureAwait(true);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("FFmpeg is already installed." + Environment.NewLine, result.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Menu_NonInteractive_Exit2()
    {
        var result = await RunCliAsync(["-m"]).ConfigureAwait(true);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Interactive mode needs a terminal.", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Cli_Doctor_FfmpegPresent_Exit0()
    {
        var script = FakeExeHarness.WriteScript([
            "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
            "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}",
            "{\"exit\":0}"
        ]);
        var wrapper = FakeExeHarness.CreateWrapper();
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
        try
        {
            var result = await RunCliAsync(["doctor", "--ffmpeg-path", wrapper]).ConfigureAwait(true);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("FFmpeg", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("libx264", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Encoders", result.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    private static async Task<CliResult> RunCliAsync(string[] arguments, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var testDirectory = Path.GetDirectoryName(typeof(SetupCliTests).Assembly.Location)!;
        var assembly = Path.Combine(testDirectory, "drawplate.dll");
        Assert.True(File.Exists(assembly), $"Expected console assembly at {assembly}");

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var dotnet = string.IsNullOrWhiteSpace(dotnetRoot)
            ? "dotnet"
            : Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (!File.Exists(dotnet))
        {
            dotnet = "dotnet";
        }

        using var process = CliProcess.Start(dotnet, [assembly, .. arguments], environment);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new CliResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private sealed record CliResult(int ExitCode, string Stdout, string Stderr);
}
