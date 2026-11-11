using System;
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
        var result = await RunCliAsync(["setup", "-n", "--ffmpeg-path", missing]);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("could not be run (file not found)", result.Stderr, StringComparison.Ordinal);
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
            var result = await RunCliAsync(["setup", "--ffmpeg-path", wrapper]);

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
        File.WriteAllText(source, "fixture");
        var missing = Path.Combine(Path.GetTempPath(), "missing-ffmpeg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await RunCliAsync(["--no-install", "--ffmpeg-path", missing, source]);

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("could not be run (file not found)", result.Stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<CliResult> RunCliAsync(string[] arguments)
    {
        var testDirectory = Path.GetDirectoryName(typeof(SetupCliTests).Assembly.Location)!;
        var assembly = Path.Combine(testDirectory, "drawplate.dll");
        Assert.True(File.Exists(assembly), $"Expected console assembly at {assembly}");

        using var process = CliProcess.Start("dotnet", [assembly, .. arguments]);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new CliResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private sealed record CliResult(int ExitCode, string Stdout, string Stderr);
}
