using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Interaction;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;
using Drawplate.Services.Setup;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Setup;

[Collection("FakeExe")]
public sealed class AcquisitionFlowTests
{
    [Fact]
    [Trait("Tier", "T1")]
    public async Task Acquisition_NoFfmpegPathArgument_DoesNotThrow()
    {
        var prompt = new TestPrompt(false, false);
        var (flow, stdout, stderr) = CreateFlow(prompt, null, OperatingSystemKind.Linux);

        var outcome = await flow.RunAsync(AcquisitionMode.Convert, false, true, null, CancellationToken.None);

        Assert.Equal(AcquisitionStatus.Exit, outcome.Status);
        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains("FFmpeg was not found.", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, prompt.ConfirmCalls);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task NeverInstall_PrintsE_FF_01_Exit3_NoPrompt()
    {
        var prompt = new TestPrompt(true, false);
        var (flow, _, stderr) = CreateFlow(prompt, null, OperatingSystemKind.Linux);

        var outcome = await flow.RunAsync(AcquisitionMode.Convert, false, true, null, CancellationToken.None);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains("Install it with your package manager", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, prompt.ConfirmCalls);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task AssumeYes_Linux_NothingRunnable_Exit3()
    {
        var (flow, _, stderr) = CreateFlow(new TestPrompt(false, false), null, OperatingSystemKind.Linux);

        var outcome = await flow.RunAsync(AcquisitionMode.SetupCommand, true, false, null, CancellationToken.None);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains("FFmpeg was not found.", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task NonInteractive_PrintsE_FF_01_Exit3()
    {
        var (flow, _, stderr) = CreateFlow(new TestPrompt(false, false), null, OperatingSystemKind.Linux);

        var outcome = await flow.RunAsync(AcquisitionMode.Convert, false, false, null, CancellationToken.None);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains("FFmpeg was not found.", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Interactive_No_Exit3()
    {
        var prompt = new TestPrompt(true, false);
        var (flow, _, stderr) = CreateFlow(prompt, null, OperatingSystemKind.MacOS);

        var outcome = await flow.RunAsync(AcquisitionMode.SetupCommand, false, false, null, CancellationToken.None);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(1, prompt.ConfirmCalls);
        Assert.Contains("FFmpeg was not found.", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Windows_NoWinget_PrintsE_FF_05()
    {
        var (flow, _, stderr) = CreateFlow(new TestPrompt(false, false), null, OperatingSystemKind.Windows);

        var outcome = await flow.RunAsync(AcquisitionMode.SetupCommand, true, false, null, CancellationToken.None);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Contains("winget was not found.", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task SetupCommand_FfmpegPresent_PrintsN_SETUP_01_Exit0()
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
            var candidate = new FfmpegCandidate(wrapper, FfmpegSource.Argument);
            var (flow, stdout, _) = CreateFlow(new TestPrompt(false, false), candidate, OperatingSystemKind.Linux);

            var outcome = await flow.RunAsync(AcquisitionMode.SetupCommand, false, true, wrapper, CancellationToken.None);

            Assert.Equal(0, outcome.ExitCode);
            Assert.Equal("FFmpeg is already installed." + Environment.NewLine, stdout.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    private static (AcquisitionFlow Flow, StringWriter Stdout, StringWriter Stderr) CreateFlow(
        IPrompt prompt,
        FfmpegCandidate? candidate,
        OperatingSystemKind os)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var tools = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.OrdinalIgnoreCase);
        var platform = new PlatformInfo(os, "test", tools, null, null, null);
        var info = new FfmpegInfoService(new ProcessLauncher());
        var flow = new AcquisitionFlow(
            info,
            prompt,
            new InstallRunner(new ProcessLauncher()),
            platform,
            stdout,
            stderr,
            _ => candidate);
        return (flow, stdout, stderr);
    }

    private sealed class TestPrompt(bool interactive, bool answer) : IPrompt
    {
        public bool IsInteractive { get; } = interactive;
        public int ConfirmCalls { get; private set; }

        public bool Confirm(string question, bool defaultYes)
        {
            ConfirmCalls++;
            return answer;
        }

        public string? Ask(string label, string? current) => current;
    }
}
