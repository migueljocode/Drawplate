using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Services.Conversion;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

/// <summary>ConversionRunner with FakeExe as FFmpeg. A fake conversion "succeeds" by writing a non-empty file to its last argument.</summary>
[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class RunnerTests
{
    private const string Banner =
        "{\"if\":\"-hide_banner -i \",\"stderr\":\"  Duration: 00:00:12.50, start: 0.000000, bitrate: 1 kb/s\\n  Stream #0:0(und): Video: h264 (High), yuv420p, 1280x720\"}\n"
        + "{\"if\":\"-hide_banner -i \",\"exit\":1}\n";

    private const string CpuSucceeds = "{\"if\":\"-progress\",\"createFile\":\"@last\",\"content\":\"data\",\"exit\":0}\n";

    private static readonly VaapiTarget Node = new("/dev/dri/renderD128", false);

    [Fact]
    public async Task Runner_VaapiFails_FallsBackToCpu()
    {
        using var rig = new Rig(Banner + "{\"if\":\"h264_vaapi\",\"stderr\":\"Cannot open the device\",\"exit\":1}\n" + CpuSucceeds);

        var result = await rig.RunAsync([EncoderKind.Vaapi, EncoderKind.Cpu], Node, rig.FakeFfmpeg).ConfigureAwait(true);

        Assert.Equal(FileResultStatus.Converted, result.Status);
        Assert.Equal("cpu", result.Encoder);
        Assert.True(File.Exists(result.Target));
        Assert.Empty(Directory.GetFiles(rig.Output, "*.partial.mp4"));
        Assert.Contains(rig.Logged, line => line.StartsWith("ffmpeg ", StringComparison.Ordinal) && line.Contains("h264_vaapi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Runner_VaapiWithoutTarget_SkipsToNextEncoder()
    {
        using var rig = new Rig(Banner + CpuSucceeds);

        var result = await rig.RunAsync([EncoderKind.Vaapi, EncoderKind.Cpu], null, rig.FakeFfmpeg).ConfigureAwait(true);

        Assert.Equal(FileResultStatus.Converted, result.Status);
        Assert.Equal("cpu", result.Encoder);
    }

    [Fact]
    public async Task Runner_StartFails_FailedResult()
    {
        using var rig = new Rig(Banner + CpuSucceeds);

        var result = await rig.RunAsync([EncoderKind.Cpu], null, Path.Combine(rig.Root, "no-such-ffmpeg")).ConfigureAwait(true);

        Assert.Equal(FileResultStatus.Failed, result.Status);
        Assert.Equal("cannot start ffmpeg", result.Reason);
        Assert.Empty(Directory.GetFiles(rig.Output));
    }

    [Fact]
    public async Task Runner_AllEncodersFail_ReasonIsLastStderrLine()
    {
        using var rig = new Rig(Banner + "{\"if\":\"-progress\",\"stderr\":\"something odd happened\",\"exit\":1}\n");

        var result = await rig.RunAsync([EncoderKind.Cpu], null, rig.FakeFfmpeg).ConfigureAwait(true);

        Assert.Equal(FileResultStatus.Failed, result.Status);
        Assert.Equal("encoder failed: something odd happened", result.Reason);
    }

    private sealed class Rig : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();
        private readonly System.Collections.Generic.List<string> _logged = [];

        public Rig(string scriptText)
        {
            File.WriteAllText(_script, scriptText);
            Root = Path.Combine(Path.GetTempPath(), "drawplate-runner-" + Guid.NewGuid().ToString("N"));
            Output = Path.Combine(Root, "out");
            Directory.CreateDirectory(Output);
            Source = Path.Combine(Root, "clip.mp4");
            File.WriteAllText(Source, "source");
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", Path.Combine(Root, "fake.log"));
        }

        public string Root { get; }

        public string Output { get; }

        public string Source { get; }

        public string FakeFfmpeg => _wrapper;

        public ImmutableArray<string> Logged => [.. _logged];

        public Task<FileResult> RunAsync(ImmutableArray<EncoderKind> chain, VaapiTarget? vaapi, string conversionFfmpeg)
        {
            // The probe always uses the working fake; only the conversion may point at something broken.
            var runner = new ConversionRunner(conversionFfmpeg, new ProcessLauncher(), new MediaProbe(new ProcessLauncher(), _wrapper, null));
            return runner.RunAsync(Source, Output, chain, 50, SizeCap.None, noTimestamps: true, vaapi, _logged.Add, progress: null, CancellationToken.None);
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
}
