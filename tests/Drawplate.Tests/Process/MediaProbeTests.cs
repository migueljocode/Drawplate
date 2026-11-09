using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class MediaProbeTests
{
    private const string ClipPath = "clip.mp4";

    [Fact]
    public async Task Banner_VideoAndAudio_DurationAndHasVideo()
    {
        using var rig = new Rig(FixtureLines("video-audio.txt"));

        var info = await rig.InspectAsync(ffprobe: null).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(12.5), info.Duration);
        Assert.True(info.HasVideo);
    }

    [Fact]
    public async Task Banner_AudioOnly_HasVideoFalse()
    {
        using var rig = new Rig(FixtureLines("audio-only.txt"));

        var info = await rig.InspectAsync(ffprobe: null).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(185.04), info.Duration);
        Assert.False(info.HasVideo);
    }

    [Fact]
    public async Task Banner_DurationNA_DurationNull()
    {
        using var rig = new Rig(FixtureLines("duration-na.txt"));

        var info = await rig.InspectAsync(ffprobe: null).ConfigureAwait(true);

        Assert.Null(info.Duration);
        Assert.True(info.HasVideo);
    }

    [Fact]
    public async Task Ffprobe_Present_UsedInsteadOfBanner()
    {
        // The banner says 12.5 s; ffprobe says 7.25 s. Only ffprobe's value may come back.
        var script = FixtureLines("video-audio.txt").Concat(
        [
            "{\"if\":\"format=duration\",\"stdout\":\"7.25\"}",
            "{\"if\":\"codec_type\",\"stdout\":\"video\"}",
        ]).ToArray();
        using var rig = new Rig(script);

        var info = await rig.InspectAsync(ffprobe: rig.Wrapper).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(7.25), info.Duration);
        Assert.True(info.HasVideo);
    }

    [Fact]
    public async Task Ffprobe_StartFails_FallsBackToBanner()
    {
        using var rig = new Rig(FixtureLines("video-audio.txt"));
        var missing = Path.Combine(Path.GetTempPath(), "no-such-ffprobe-" + Guid.NewGuid().ToString("N"));

        var info = await rig.InspectAsync(ffprobe: missing).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(12.5), info.Duration);
        Assert.True(info.HasVideo);
    }

    [Fact]
    public void FfprobeLocator_Sibling_Found()
    {
        // Build every path with Path.Combine so Windows separators match what GetDirectoryName returns.
        var folder = Path.Combine(Path.GetTempPath(), "ffmpeg-bin");
        var ffmpeg = Path.Combine(folder, "ffmpeg");
        var expected = Path.Combine(folder, "ffprobe");

        var found = FfprobeLocator.Find(ffmpeg, [Path.GetTempPath()], p => p == expected, OperatingSystemKind.Linux);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void FfprobeLocator_None_ReturnsNull()
    {
        var ffmpeg = Path.Combine(Path.GetTempPath(), "ffmpeg-bin", "ffmpeg");

        var found = FfprobeLocator.Find(ffmpeg, [Path.GetTempPath()], _ => false, OperatingSystemKind.Linux);

        Assert.Null(found);
    }

    private static string[] FixtureLines(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(MediaProbeTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoDir, "tests", "Drawplate.Tests", "Fixtures", "banner", name);
        return [.. File.ReadAllLines(path).Select(line => JsonSerializer.Serialize(new Dictionary<string, object> { ["stderr"] = line }))];
    }

    /// <summary>FakeExe as ffmpeg: the banner script is shared by every run, so ffprobe lines are guarded with "if".</summary>
    private sealed class Rig : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _log = Path.GetTempFileName();

        public Rig(string[] scriptLines)
        {
            File.WriteAllLines(_script, scriptLines);
            Wrapper = FakeExeHarness.CreateWrapper();
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", _log);
        }

        public string Wrapper { get; }

        public Task<MediaInfo> InspectAsync(string? ffprobe)
        {
            var probe = new MediaProbe(new ProcessLauncher(), Wrapper, ffprobe);
            return probe.InspectAsync(ClipPath, CancellationToken.None);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", null);
            File.Delete(_script);
            File.Delete(_log);
            File.Delete(Wrapper);
        }
    }
}
