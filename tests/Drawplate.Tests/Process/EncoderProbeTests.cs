using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Ffmpeg;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class EncoderProbeTests
{
    private static readonly string[] OneNode = ["/dev/dri/renderD128"];

    [Fact]
    public async Task Probe_HardwareNotListed_NoProcess()
    {
        using var rig = new Rig("{\"exit\":0}");

        var results = await ProbeAsync(rig.Info("libx264"), [EncoderKind.Nvenc]).ConfigureAwait(true);

        Assert.False(results[0].Passed);
        Assert.Equal(ProbeFailureKind.NotListed, results[0].FailureKind);
        Assert.Equal(0, rig.StartedProcesses);
    }

    [Fact]
    public async Task Probe_Vaapi_NoDevices_NoDevice_NoProcess()
    {
        using var rig = new Rig("{\"exit\":0}");

        var results = await ProbeAsync(rig.Info("libx264", "h264_vaapi"), [EncoderKind.Vaapi], listDirectory: _ => []).ConfigureAwait(true);

        Assert.Equal(ProbeFailureKind.NoDevice, results[0].FailureKind);
        Assert.Equal(0, rig.StartedProcesses);
    }

    [Fact]
    public async Task Probe_Vaapi_FirstNodeFails_SecondPasses_UsesSecond()
    {
        using var rig = new Rig("{\"if\":\"renderD129\",\"exit\":0}", "{\"exit\":1}");

        // Listed out of order on purpose: the probe sorts them, so renderD128 is tried first.
        var results = await ProbeAsync(
            rig.Info("libx264", "h264_vaapi"),
            [EncoderKind.Vaapi],
            listDirectory: _ => ["/dev/dri/renderD129", "/dev/dri/renderD128"]).ConfigureAwait(true);

        Assert.True(results[0].Passed);
        Assert.Equal("/dev/dri/renderD129", results[0].Vaapi!.Device);
        Assert.Equal(3, rig.StartedProcesses);
    }

    [Fact]
    public async Task Probe_Vaapi_StandardFails_LowPowerPasses()
    {
        using var rig = new Rig("{\"if\":\"-low_power\",\"exit\":0}", "{\"exit\":1}");

        var results = await ProbeAsync(rig.Info("libx264", "h264_vaapi"), [EncoderKind.Vaapi], listDirectory: _ => OneNode).ConfigureAwait(true);

        Assert.True(results[0].Passed);
        Assert.True(results[0].Vaapi!.LowPower);
        Assert.Equal(2, rig.StartedProcesses);
    }

    [Fact]
    public async Task Probe_Vaapi_VaapiTargetRecorded()
    {
        using var rig = new Rig("{\"exit\":0}");

        var results = await ProbeAsync(rig.Info("libx264", "h264_vaapi"), [EncoderKind.Vaapi], listDirectory: _ => OneNode).ConfigureAwait(true);

        Assert.Equal(new VaapiTarget("/dev/dri/renderD128", false), results[0].Vaapi);
    }

    [Fact]
    public async Task Probe_OnlyRequestedKinds_LogsOneProbe()
    {
        using var rig = new Rig("{\"exit\":0}");
        var logged = new List<string>();

        var results = await ProbeAsync(rig.Info("libx264", "h264_nvenc"), [EncoderKind.Cpu], log: logged.Add).ConfigureAwait(true);

        Assert.Single(results);
        Assert.Equal(1, rig.StartedProcesses);
        Assert.Single(logged);
        Assert.Contains("libx264", logged[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probe_Hang_KilledAtTimeout()
    {
        using var rig = new Rig("{\"hang\":true}");
        var started = Stopwatch.GetTimestamp();

        var results = await ProbeAsync(rig.Info("libx264"), [EncoderKind.Cpu], timeout: TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        Assert.False(results[0].Passed);
        Assert.Equal(ProbeFailureKind.Unknown, results[0].FailureKind);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Probe_Cpu_NoLibx264_NoProcess()
    {
        using var rig = new Rig("{\"exit\":0}");

        var results = await ProbeAsync(rig.Info("h264_nvenc"), [EncoderKind.Cpu]).ConfigureAwait(true);

        Assert.Equal(ProbeFailureKind.EncoderUnavailable, results[0].FailureKind);
        Assert.Equal(0, rig.StartedProcesses);
    }

    [Fact]
    public async Task Probe_StartFails_ReturnsFailedNotThrow()
    {
        using var rig = new Rig("{\"exit\":0}");
        var missing = Path.Combine(Path.GetTempPath(), "no-such-ffmpeg-" + Guid.NewGuid().ToString("N"));
        var info = new FfmpegInfo(missing, new FfmpegVersion(new Version(6, 1, 1), false, string.Empty), ImmutableHashSet.Create("libx264"), true);

        var results = await ProbeAsync(info, [EncoderKind.Cpu]).ConfigureAwait(true);

        Assert.False(results[0].Passed);
        Assert.Equal(ProbeFailureKind.Unknown, results[0].FailureKind);
    }

    [Fact]
    public async Task Probe_Cancelled_Throws()
    {
        using var rig = new Rig("{\"hang\":true}");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ProbeAsync(rig.Info("libx264"), [EncoderKind.Cpu], ct: cts.Token)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("nvenc-no-libcuda.txt", EncoderKind.Nvenc, 255)]
    [InlineData("qsv-mfx-session.txt", EncoderKind.Qsv, 171)]
    [InlineData("vaapi-no-display.txt", EncoderKind.Vaapi, 234)]
    public async Task Probe_RealStderrFixtures_AllFail(string fixture, EncoderKind kind, int exitCode)
    {
        var text = (await File.ReadAllTextAsync(FixturePath(fixture)).ConfigureAwait(true)).Replace("\r\n", "\n").TrimEnd('\n');
        using var rig = new Rig(JsonSerializer.Serialize(new Dictionary<string, object> { ["stderr"] = text, ["exit"] = exitCode }));
        var info = rig.Info("libx264", "h264_nvenc", "h264_qsv", "h264_vaapi");

        var results = await ProbeAsync(info, [kind], listDirectory: _ => OneNode).ConfigureAwait(true);

        Assert.False(results[0].Passed);
        Assert.NotNull(results[0].FailureKind);
    }

    private static Task<ImmutableArray<ProbeResult>> ProbeAsync(
        FfmpegInfo info,
        ImmutableArray<EncoderKind> kinds,
        TimeSpan? timeout = null,
        Action<string>? log = null,
        Func<string, IEnumerable<string>>? listDirectory = null,
        CancellationToken ct = default)
    {
        var probe = new EncoderProbe(new ProcessLauncher(), new FfmpegInfoService(new ProcessLauncher()), OperatingSystemKind.Linux, timeout, log, listDirectory);
        return probe.ProbeAsync(info, kinds, ct);
    }

    private static string FixturePath(string fixture)
    {
        var testDir = Path.GetDirectoryName(typeof(EncoderProbeTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoDir, "tests", "Drawplate.Tests", "Fixtures", "stderr", fixture);
    }

    /// <summary>FakeExe as ffmpeg: a script, a log of started processes, and cleanup.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _log = Path.GetTempFileName();
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();

        public Rig(params string[] scriptLines)
        {
            File.WriteAllLines(_script, scriptLines);
            File.Delete(_log);
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", _log);
        }

        public int StartedProcesses => File.Exists(_log) ? File.ReadAllLines(_log).Length : 0;

        public FfmpegInfo Info(params string[] encoders) =>
            new(_wrapper, new FfmpegVersion(new Version(6, 1, 1), false, string.Empty), ImmutableHashSet.Create(encoders), encoders.Contains("libx264"));

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", null);
            File.Delete(_script);
            File.Delete(_log);
            File.Delete(_wrapper);
        }
    }
}
