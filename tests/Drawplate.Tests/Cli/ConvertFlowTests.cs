using System;
using System.IO;
using System.Threading;

using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>Drives AppHost in-process with FakeExe as FFmpeg; the FakeExe log shows whether any process started.</summary>
[Collection("FakeExe")]
[Trait("Tier", "T1")]
public class ConvertFlowTests
{
    private const string InfoScript = "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\"}\n{\"exit\":0}\n";

    [Fact]
    public void Flow_OutputFlag_Honored()
    {
        using var run = new Scenario(InfoScript);
        var input = run.Folder("input");
        File.WriteAllText(Path.Combine(input, "a.mp4"), string.Empty);
        var output = Path.Combine(run.Root, "custom-out");

        run.Run("-i", input, "-o", output);

        Assert.True(Directory.Exists(output));
    }

    [Fact]
    public void Flow_OutputIsFile_E_USAGE_08_NoProcess()
    {
        using var run = new Scenario(InfoScript);
        var input = run.Folder("input");
        File.WriteAllText(Path.Combine(input, "a.mp4"), string.Empty);
        var output = Path.Combine(run.Root, "not-a-folder");
        File.WriteAllText(output, string.Empty);

        var result = run.Run("-i", input, "-o", output);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Output path is a file, not a folder:", result.Stderr);
        Assert.False(run.ProcessStarted);
    }

    [Fact]
    public void Flow_Collision_E_USAGE_09_NoProcess()
    {
        using var run = new Scenario(InfoScript);
        var x = run.Folder("x");
        var y = run.Folder("y");
        File.WriteAllText(Path.Combine(x, "clip.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(y, "clip.mov"), string.Empty);

        var result = run.Run(Path.Combine(x, "clip.mp4"), Path.Combine(y, "clip.mov"), "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("More than one input would write the same output file:", result.Stderr);
        Assert.False(run.ProcessStarted);
    }

    [Fact]
    public void Flow_UnsupportedOnly_E_RUN_01_Exit1()
    {
        using var run = new Scenario(InfoScript);
        var notes = Path.Combine(run.Root, "notes.txt");
        File.WriteAllText(notes, string.Empty);

        var result = run.Run(notes, "-o", Path.Combine(run.Root, "out"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No supported video files found.", result.Stderr);
        Assert.False(run.ProcessStarted);
    }

    [Fact]
    public void Flow_OutputEqualsInput_AllSkipped_Exit0()
    {
        using var run = new Scenario(InfoScript);
        var input = run.Folder("input");
        File.WriteAllText(Path.Combine(input, "a.mp4"), string.Empty);

        var result = run.Run("-i", input, "-o", input);

        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>A temp root, a FakeExe wrapper as --ffmpeg-path, and a log that records started processes.</summary>
    private sealed class Scenario : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _log;
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();

        public Scenario(string scriptText)
        {
            File.WriteAllText(_script, scriptText);
            Root = Path.Combine(Path.GetTempPath(), "drawplate-flow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _log = Path.Combine(Root, "fake.log");
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", _script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", _log);
        }

        public string Root { get; }

        public bool ProcessStarted => File.Exists(_log);

        public string Folder(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

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
