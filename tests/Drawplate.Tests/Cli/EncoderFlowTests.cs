using System;
using System.IO;
using System.Threading;

using Drawplate.Services.Platform;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>Encoder selection through AppHost. FakeExe answers the probes by encoder name in the argv.</summary>
[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class EncoderFlowTests
{
    private const string InfoLines =
        "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\"}\n"
        + "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n V..... h264_nvenc\\n V..... h264_qsv\\n V..... h264_vaapi\\n V..... h264_amf\\n V..... h264_videotoolbox\"}\n";

    private const string HardwareFails =
        "{\"if\":\"h264_nvenc\",\"exit\":1}\n"
        + "{\"if\":\"h264_qsv\",\"exit\":1}\n"
        + "{\"if\":\"h264_vaapi\",\"exit\":1}\n"
        + "{\"if\":\"h264_amf\",\"exit\":1}\n"
        + "{\"if\":\"h264_videotoolbox\",\"exit\":1}\n";

    [Fact]
    public void Flow_AllHardwareFails_GpuPresent_OneNote()
    {
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");
        var input = run.Input();
        GpuDetector.Override = () => true;
        try
        {
            // The fake conversion writes no output, so the exit code is not asserted; the note count is.
            var result = run.Run("-i", input, "-o", Path.Combine(run.Root, "out"));

            Assert.NotEqual(2, result.ExitCode);
            Assert.Single(result.Stdout.Split('\n'), line => line.StartsWith("Note:", StringComparison.Ordinal));
        }
        finally
        {
            GpuDetector.Override = null;
        }
    }

    [Fact]
    public void Flow_AllHardwareFails_NoGpu_NoNote()
    {
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");
        var input = run.Input();
        GpuDetector.Override = () => false;
        try
        {
            var result = run.Run("-i", input, "-o", Path.Combine(run.Root, "out"));

            Assert.DoesNotContain("Note:", result.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            GpuDetector.Override = null;
        }
    }

    [Fact]
    public void Flow_CpuFlag_NoHardwareProbe_NoNote()
    {
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");
        var input = run.Input();
        GpuDetector.Override = () => true;
        try
        {
            var result = run.Run("-c", "-i", input, "-o", Path.Combine(run.Root, "out"));

            Assert.DoesNotContain("Note:", result.Stdout, StringComparison.Ordinal);
            Assert.False(run.LogContains("h264_nvenc"));
        }
        finally
        {
            GpuDetector.Override = null;
        }
    }

    [Fact]
    public void Flow_EncoderNvencBroken_E_RUN_02()
    {
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");
        var input = run.Input();

        // Each OS has its own hardware encoder; asking for one that is listed but broken is E-RUN-02.
        var id = OperatingSystem.IsMacOS() ? "videotoolbox" : "nvenc";

        var result = run.Run("-e", id, "-i", input, "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"Encoder '{id}' is not working on this machine.", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_NoLibx264_NoHardware_E_FF_03()
    {
        using var run = new Scenario(
            "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\"}\n"
            + "{\"if\":\"-encoders\",\"stdout\":\" V..... h264_nvenc\"}\n"
            + "{\"if\":\"h264_nvenc\",\"exit\":1}\n"
            + "{\"exit\":0}\n");
        var input = run.Input();

        var result = run.Run("-i", input, "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("no libx264 encoder", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_ListEncoders_IdsOnStdout_NoInputNeeded()
    {
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");

        var result = run.Run("-l");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("cpu\n", result.Stdout.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Flow_MaxSize_Accepted_NotUsageError()
    {
        // The plan card goes straight to the console and the fake media has no video, so only the parse result is observable here.
        using var run = new Scenario(InfoLines + HardwareFails + "{\"exit\":0}\n");
        var input = run.Input();

        var result = run.Run("-s", "hd", "-i", input, "-o", Path.Combine(run.Root, "out"));

        Assert.NotEqual(2, result.ExitCode);
    }

    /// <summary>A temp root with an input folder, FakeExe as ffmpeg, and FakeExe's start log.</summary>
    private sealed class Scenario : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();
        private readonly string _log;

        public Scenario(string scriptText)
        {
            File.WriteAllText(_script, scriptText);
            Root = Path.Combine(Path.GetTempPath(), "drawplate-encoder-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _log = Path.Combine(Root, "fake.log");
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", _log);
        }

        public string Root { get; }

        public string Input()
        {
            var folder = Path.Combine(Root, "in");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "a.mp4"), string.Empty);
            return folder;
        }

        public bool LogContains(string text) => File.Exists(_log) && File.ReadAllText(_log).Contains(text, StringComparison.Ordinal);

        public Result Run(params string[] args)
        {
            var argv = new string[args.Length + 2];
            argv[0] = "--ffmpeg-path";
            argv[1] = _wrapper;
            Array.Copy(args, 0, argv, 2, args.Length);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exit = Drawplate.Console.AppHost.Run(argv, stdout, stderr, CancellationToken.None);
            return new Result(exit, stdout.ToString(), stderr.ToString());
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", null);
            File.Delete(_script);
            File.Delete(_wrapper);
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record Result(int ExitCode, string Stdout, string Stderr);
}
