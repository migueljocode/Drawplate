using System;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>The whole convert flow in-process with FakeExe as FFmpeg (R12). Real FFmpeg proof is R16.</summary>
[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class ConversionFlowTests
{
    // Order matters: FakeExe runs the first matching line that exits. Banner first, then the failing file, then success.
    private const string Script =
        "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\"}\n"
        + "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\"}\n"
        + "{\"if\":\"-hide_banner -i \",\"stderr\":\"  Duration: 00:00:01.00, start: 0.000000, bitrate: 1 kb/s\\n  Stream #0:0: Video: h264, yuv420p, 320x180\"}\n"
        + "{\"if\":\"-hide_banner -i \",\"exit\":1}\n"
        + "{\"if\":\"bad.mp4\",\"stderr\":\"Invalid data found when processing input\",\"exit\":1}\n"
        + "{\"if\":\"-progress\",\"createFile\":\"@last\",\"content\":\"data\",\"exit\":0}\n"
        + "{\"exit\":0}\n";

    [Fact]
    public void Flow_OneBadFile_OthersContinue_Exit1()
    {
        using var run = new Scenario(Script);
        var input = run.Input("bad.mp4", "good.mp4");
        var output = Path.Combine(run.Root, "out");

        var result = run.Run("-i", input, "-o", output);

        Assert.Equal(1, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(output, "good.mp4")));
        Assert.False(File.Exists(Path.Combine(output, "bad.mp4")));
        Assert.Empty(Directory.GetFiles(output, "*.partial.mp4"));
        Assert.Contains("[1/2] bad.mp4  invalid or corrupt input", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("[2/2] good.mp4  ", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Result    1 converted   1 failed", result.Stdout, StringComparison.Ordinal);
        Assert.EndsWith("Finished with 1 failure(s).\n", result.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_Rerun_SkipsExisting_Exit0()
    {
        using var run = new Scenario(Script);
        var input = run.Input("good.mp4");
        var output = Path.Combine(run.Root, "out");

        var first = run.Run("-i", input, "-o", output);
        var conversionsAfterFirst = run.ConversionCount();
        var second = run.Run("-i", input, "-o", output);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(1, conversionsAfterFirst);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(1, run.ConversionCount());
        Assert.Contains("[1/1] good.mp4  skipped: output already exists", second.Stdout, StringComparison.Ordinal);
        Assert.Contains("Result    0 converted   1 skipped", second.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_PlanCard_ShowsRealValues()
    {
        using var run = new Scenario(Script);
        var input = run.Input("good.mp4");

        var result = run.Run("-q", "80", "-s", "hd", "-i", input, "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Quality   80 (CRF 11)", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Max size  hd 1280x720", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Encoder   [CPU]", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Encoders  [CPU] x1", result.Stdout, StringComparison.Ordinal);
        Assert.True(run.LogLines().Any(l => l.Contains("-progress", StringComparison.Ordinal) && l.Contains("min(iw,1280)", StringComparison.Ordinal)));
    }

    [Fact]
    public void Flow_Verbose_PrintsFfmpegArgvToStderr()
    {
        using var run = new Scenario(Script);
        var input = run.Input("good.mp4");

        var result = run.Run("--verbose", "-i", input, "-o", Path.Combine(run.Root, "out"));

        Assert.Contains("ffmpeg -nostdin -hide_banner -loglevel error", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_FilesMode_PlanCardCountsFiles()
    {
        using var run = new Scenario(Script);
        var input = run.Input("good.mp4");

        var result = run.Run(Path.Combine(input, "good.mp4"), "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Input     1 file(s)", result.Stdout, StringComparison.Ordinal);
    }

    private sealed class Scenario : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();
        private readonly string _log;

        public Scenario(string scriptText)
        {
            File.WriteAllText(_script, scriptText);
            Root = Path.Combine(Path.GetTempPath(), "drawplate-conv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _log = Path.Combine(Root, "fake.log");
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", _log);
        }

        public string Root { get; }

        public string Input(params string[] names)
        {
            var folder = Path.Combine(Root, "in");
            Directory.CreateDirectory(folder);
            foreach (var name in names)
            {
                File.WriteAllText(Path.Combine(folder, name), "source");
            }

            return folder;
        }

        public string[] LogLines() => File.Exists(_log) ? File.ReadAllLines(_log) : [];

        public int ConversionCount() => LogLines().Count(l => l.Contains("-progress", StringComparison.Ordinal));

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
