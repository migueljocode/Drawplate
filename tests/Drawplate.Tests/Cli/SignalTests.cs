using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>
/// F23 on Unix: signal the real drawplate process (its PID only) while a FakeExe "ffmpeg" hangs on the conversion.
/// Expect exit 130 within 5 s, no partial file and no FakeExe process left.
/// </summary>
[Trait("Tier", "T1")]
public class SignalTests
{
    private const string HangScript =
        "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\"}\n"
        + "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\"}\n"
        + "{\"if\":\"-hide_banner -i \",\"stderr\":\"  Duration: 00:00:01.00, start: 0.000000, bitrate: 1 kb/s\\n  Stream #0:0: Video: h264, yuv420p, 320x180\"}\n"
        + "{\"if\":\"-hide_banner -i \",\"exit\":1}\n"
        + "{\"if\":\"-progress\",\"createFile\":\"@last\",\"content\":\"partial\"}\n"
        + "{\"if\":\"-progress\",\"hang\":true}\n"
        + "{\"exit\":0}\n";

    [UnixFact]
    public void Sigterm_ToPidOnly_Exit130Within5s()
    {
        using var run = HungRun.Start();

        var result = run.SignalAndWait("TERM");

        Assert.Equal(130, result.ExitCode);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(5), $"took {result.Elapsed.TotalSeconds:0.0} s");
        Assert.Contains("Interrupted.", result.Stderr, StringComparison.Ordinal);
    }

    [UnixFact]
    public void Sigint_Exit130()
    {
        using var run = HungRun.Start();

        var result = run.SignalAndWait("INT");

        Assert.Equal(130, result.ExitCode);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(5), $"took {result.Elapsed.TotalSeconds:0.0} s");
    }

    [UnixFact]
    public void Sighup_Exit130()
    {
        using var run = HungRun.Start();

        var result = run.SignalAndWait("HUP");

        Assert.Equal(130, result.ExitCode);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(5), $"took {result.Elapsed.TotalSeconds:0.0} s");
    }

    [UnixFact]
    public void Cancel_DeletesPartial()
    {
        using var run = HungRun.Start();
        Assert.Single(Directory.GetFiles(run.Output, "*.partial.mp4"));

        run.SignalAndWait("TERM");

        Assert.Empty(Directory.GetFiles(run.Output));
    }

    [UnixFact]
    public void Cancel_NoChildLeft()
    {
        using var run = HungRun.Start();

        run.SignalAndWait("TERM");

        // Every started process carries the unique temp folder in its argv, so pgrep finds any survivor.
        Assert.True(run.WaitUntilNoProcessMentions(TimeSpan.FromSeconds(5)), "a FakeExe or drawplate process is still running");
    }

    public sealed class UnixFactAttribute : FactAttribute
    {
        public UnixFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                Skip = "Needs POSIX signals.";
            }
        }
    }

    private sealed record Outcome(int ExitCode, string Stderr, TimeSpan Elapsed);

    private sealed class HungRun : IDisposable
    {
        private readonly string _script = Path.GetTempFileName();
        private readonly string _wrapper = FakeExeHarness.CreateWrapper();
        private readonly Process _process;
        private readonly List<string> _stderr = [];

        private HungRun(string root)
        {
            Root = root;
            Output = Path.Combine(root, "out");
            Log = Path.Combine(root, "fake.log");
            File.WriteAllText(_script, HangScript);

            var input = Path.Combine(root, "in");
            Directory.CreateDirectory(input);
            File.WriteAllText(Path.Combine(input, "good.mp4"), "source");

            var psi = new ProcessStartInfo
            {
                FileName = DrawplateExe(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in new[] { "--ffmpeg-path", _wrapper, "-i", input, "-o", Output })
            {
                psi.ArgumentList.Add(arg);
            }

            psi.Environment["FAKE_EXE_SCRIPT"] = _script;
            psi.Environment["FAKE_EXE_LOG"] = Log;

            _process = Process.Start(psi)!;
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, e) =>
            {
                lock (_stderr)
                {
                    _stderr.Add(e.Data ?? string.Empty);
                }
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public string Root { get; }

        public string Output { get; }

        private string Log { get; }

        /// <summary>Starts drawplate and waits until the hanging fake conversion is running and its partial file exists.</summary>
        public static HungRun Start()
        {
            var root = Path.Combine(Path.GetTempPath(), "drawplate-signal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var run = new HungRun(root);
            run.WaitForConversion();
            return run;
        }

        public Outcome SignalAndWait(string signal)
        {
            var clock = Stopwatch.StartNew();
            var psi = new ProcessStartInfo { FileName = "kill", UseShellExecute = false };
            psi.ArgumentList.Add("-" + signal);
            psi.ArgumentList.Add(_process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using (var kill = Process.Start(psi)!)
            {
                kill.WaitForExit();
            }

            Assert.True(_process.WaitForExit(15000), "drawplate did not exit");
            var elapsed = clock.Elapsed;
            _process.WaitForExit();
            lock (_stderr)
            {
                return new Outcome(_process.ExitCode, string.Join("\n", _stderr), elapsed);
            }
        }

        public bool WaitUntilNoProcessMentions(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                var psi = new ProcessStartInfo { FileName = "pgrep", UseShellExecute = false, RedirectStandardOutput = true };
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add(Root);
                using var pgrep = Process.Start(psi)!;
                pgrep.StandardOutput.ReadToEnd();
                pgrep.WaitForExit();
                if (pgrep.ExitCode == 1)
                {
                    return true;
                }

                if (DateTime.UtcNow > deadline)
                {
                    return false;
                }

                Thread.Sleep(100);
            }
        }

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit();
            }

            _process.Dispose();
            File.Delete(_script);
            File.Delete(_wrapper);
            Directory.Delete(Root, recursive: true);
        }

        private void WaitForConversion()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                var started = File.Exists(Log) && File.ReadAllLines(Log).Any(l => l.Contains("-progress", StringComparison.Ordinal));
                if (started && Directory.Exists(Output) && Directory.GetFiles(Output, "*.partial.mp4").Length == 1)
                {
                    return;
                }

                Thread.Sleep(100);
            }

            throw new InvalidOperationException("The fake conversion never started.");
        }

        private static string DrawplateExe()
        {
            var tfmDir = Path.GetDirectoryName(typeof(SignalTests).Assembly.Location)!;
            var configuration = Path.GetFileName(Path.GetDirectoryName(tfmDir)!);
            var repo = Path.GetFullPath(Path.Combine(tfmDir, "..", "..", "..", "..", ".."));
            var path = Path.Combine(repo, "src", "Drawplate.Console", "bin", configuration, "net10.0", "drawplate");
            return File.Exists(path) ? path : throw new InvalidOperationException("Build src/Drawplate.Console first");
        }
    }
}
