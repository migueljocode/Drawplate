using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;
using Drawplate.Services.Setup;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Setup;

[Collection("FakeExe")]
public sealed class DoctorTests
{
    [Fact]
    [Trait("Tier", "T1")]
    public async Task AllOk_Exit0_ExactLines()
    {
        var report = await RunAsync(InfoScript(true), gpu: false).ConfigureAwait(true);

        Assert.Equal(0, report.ExitCode);
        Assert.Collection(
            report.Lines,
            line => { Assert.Equal("ok", line.Status); Assert.Equal("FFmpeg", line.Check); Assert.Contains("found on --ffmpeg-path", line.Detail, StringComparison.Ordinal); },
            line => { Assert.Equal("ok", line.Status); Assert.Equal("libx264", line.Check); Assert.Equal("present", line.Detail); },
            line => { Assert.Equal("ok", line.Status); Assert.Equal("ffprobe", line.Check); },
            line => { Assert.Equal("ok", line.Status); Assert.Equal("Encoders", line.Check); Assert.Equal("cpu", line.Detail); });
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Missing_ShowsE_FF_01_Exit3()
    {
        var report = await RunAsync(null, gpu: false, missing: true).ConfigureAwait(true);

        Assert.Equal(3, report.ExitCode);
        Assert.Equal(new DoctorLine("missing", "FFmpeg", "not found"), Assert.Single(report.Lines));
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Broken_ShowsE_FF_02_Exit3()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = await RunAsync(null, gpu: false, candidate: new FfmpegCandidate(missingPath, FfmpegSource.Argument)).ConfigureAwait(true);

        Assert.Equal(3, report.ExitCode);
        Assert.Equal("broken", Assert.Single(report.Lines).Status);
        Assert.Contains("could not be run (file not found)", report.Error!.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Outdated_Exit3()
    {
        var report = await RunAsync("{\"if\":\"-version\",\"stdout\":\"ffmpeg version 5.1.1\\n\"}\n{\"exit\":0}", gpu: false).ConfigureAwait(true);

        Assert.Equal(3, report.ExitCode);
        Assert.Equal("outdated", Assert.Single(report.Lines).Status);
        Assert.Contains("older than the supported minimum", report.Error!.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task NoFfprobe_StillOk()
    {
        var report = await RunAsync(InfoScript(true), gpu: false, noFfprobe: true).ConfigureAwait(true);

        Assert.Equal(0, report.ExitCode);
        var ffprobe = Assert.Single(report.Lines, line => line.Check == "ffprobe");
        Assert.Equal("ok", ffprobe.Status);
        Assert.Equal("not found (optional; durations come from ffmpeg)", ffprobe.Detail);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task NoLibx264_NoHwEncoder_Broken_E_FF_03()
    {
        var script = "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\"\"}\n{\"exit\":0}";
        var report = await RunAsync(script, gpu: false).ConfigureAwait(true);

        Assert.Equal(3, report.ExitCode);
        Assert.Equal("broken", report.Lines[^1].Status);
        Assert.Equal("Encoders", report.Lines[^1].Check);
        Assert.Contains("no libx264 encoder", report.Error!.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Note_DoesNotChangeExit()
    {
        var report = await RunAsync(InfoScript(true), gpu: true).ConfigureAwait(true);

        Assert.Equal(0, report.ExitCode);
        var note = Assert.Single(report.Lines, line => line.Status == "note");
        Assert.Equal("Hardware", note.Check);
        Assert.Contains("no GPU encoder is available", note.Detail, StringComparison.Ordinal);
    }

    private static string InfoScript(bool libx264) =>
        "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n" +
        "{\"if\":\"-encoders\",\"stdout\":\"" + (libx264 ? " V..... libx264\\n" : "") + "\"}\n" +
        "{\"exit\":0}";

    private static async Task<DoctorReport> RunAsync(
        string? script,
        bool gpu,
        bool missing = false,
        FfmpegCandidate? candidate = null,
        bool noFfprobe = false)
    {
        var oldGpu = GpuDetector.Override;
        var scriptPath = script is null ? null : FakeExeHarness.WriteScript(script.Split('\n'));
        var wrapper = script is null ? null : FakeExeHarness.CreateWrapper();
        if (scriptPath is not null)
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", scriptPath);
        }

        GpuDetector.Override = () => gpu;
        try
        {
            var platform = new PlatformInfo(
                OperatingSystemKind.Linux, "test",
                System.Collections.Immutable.ImmutableHashSet<string>.Empty,
                null, null, null);
            var service = new DoctorService(
                new FfmpegInfoService(new ProcessLauncher()),
                new ProcessLauncher(),
                platform,
                _ => missing ? null : candidate ?? new FfmpegCandidate(wrapper!, FfmpegSource.Argument),
                noFfprobe ? _ => null : null);
            return await service.RunAsync(wrapper, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            GpuDetector.Override = oldGpu;
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            if (scriptPath is not null) File.Delete(scriptPath);
            if (wrapper is not null) File.Delete(wrapper);
        }
    }
}
