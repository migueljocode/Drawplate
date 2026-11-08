using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
public class LauncherTests
{
    private static string FakeExePath =>
        Path.Combine(GetRepoDir(), "tests", "FakeExe", "bin", "Release", "net10.0", "FakeExe.dll");

    private static string GetRepoDir()
    {
        var testDir = Path.GetDirectoryName(typeof(LauncherTests).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
    }

    [Fact]
    public async Task Stdout_IsStreamedLineByLine()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"stdout\":\"line1\"}\n{\"stdout\":\"line2\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        var log = Path.GetTempFileName();

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", log);
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions());

            var lines = new List<string>();
            await foreach (var line in process.StdoutLines.ConfigureAwait(true))
            {
                lines.Add(line);
            }

            var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exit);
            Assert.Equal(["line1", "line2"], lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", null);
            File.Delete(script);
            File.Delete(log);
        }
    }

    [Fact]
    public async Task Stderr_TailIsBounded_At64KiB()
    {
        var script = Path.GetTempFileName();
        var bigLine = new string('x', 100000);
        await File.WriteAllTextAsync(script, $"{{\"stderr\":\"{bigLine}\"}}\n{{\"exit\":0}}\n").ConfigureAwait(true);

        try
        {
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions());

            var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exit);

            var tail = process.StderrTail;
            Assert.True(tail.Length <= 65536);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public async Task Timeout_KillsAndReportsTimedOut()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"hang\":true}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions(TimeSpan.FromMilliseconds(100)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var exit = await process.WaitAsync(cts.Token).ConfigureAwait(true);

            Assert.True(process.TimedOut || exit == -1);
            Assert.Equal(-1, exit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task Cancel_KillsProcessTree_Within3Seconds()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"hang\":true}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            var launcher = new ProcessLauncher();
            using var cts = new CancellationTokenSource();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions());

            var waitTask = process.WaitAsync(cts.Token);
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await process.WaitAsync(cts.Token);
            }).ConfigureAwait(true);

            // Process should be dead within 3 seconds
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (process.ExitCode != -1)
                {
                    break;
                }
                await Task.Delay(50).ConfigureAwait(true);
            }

            Assert.NotEqual(-1, process.ExitCode);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExitCode_IsReturned()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"exit\":7}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions());

            var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(7, exit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ArgumentsWithSpacesAndQuotes_ArriveIntact()
    {
        var script = Path.GetTempFileName();
        var log = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"exit\":0}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", log);
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath, "my arg with spaces", "quoted\"arg"], new ProcessOptions());

            var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exit);

            var logContent = await File.ReadAllTextAsync(log).ConfigureAwait(true);
            var parts = logContent.TrimEnd('\n').Split('\u001F');
            Assert.Contains("my arg with spaces", parts);
            Assert.Contains("quoted\"arg", parts);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            Environment.SetEnvironmentVariable("FAKE_EXE_LOG", null);
            File.Delete(script);
            File.Delete(log);
        }
    }

    [Fact]
    public async Task Timeout_WhileStdoutOpen_KillsAndReportsTimedOut()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"stdout\":\"started\"}\n{\"hang\":true}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            var started = Stopwatch.GetTimestamp();
            await using var process = new ProcessLauncher().Start("dotnet", [FakeExePath], new ProcessOptions(TimeSpan.FromSeconds(1)));

            var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(-1, exit);
            Assert.True(process.TimedOut);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task Cancel_WhileDrainingStdout_KillsWithinThreeSeconds()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"stdout\":\"started\"}\n{\"hang\":true}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            using var cts = new CancellationTokenSource();
            await using var process = new ProcessLauncher().Start("dotnet", [FakeExePath], new ProcessOptions());

            var drained = new List<string>();
            var drain = Task.Run(async () =>
            {
                await foreach (var line in process.StdoutLines.ConfigureAwait(false))
                {
                    drained.Add(line);
                }
            });

            cts.CancelAfter(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.WaitAsync(cts.Token)).ConfigureAwait(true);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline && process.ExitCode == -1)
            {
                await Task.Delay(50).ConfigureAwait(true);
            }

            Assert.NotEqual(-1, process.ExitCode);
            await drain.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            Assert.Equal(["started"], drained);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task Wait_AfterManyStderrLines_TailHasLastLine()
    {
        var script = Path.GetTempFileName();
        var lines = Enumerable.Range(0, 2000).Select(i => $"{{\"stderr\":\"line {i}\"}}").Append("{\"exit\":0}");
        await File.WriteAllLinesAsync(script, lines).ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            for (var round = 0; round < 20; round++)
            {
                await using var process = new ProcessLauncher().Start("dotnet", [FakeExePath], new ProcessOptions());

                var exit = await process.WaitAsync(CancellationToken.None).ConfigureAwait(true);

                Assert.Equal(0, exit);
                Assert.Contains("line 1999", process.StderrTail);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }

    [Fact]
    public void Start_MissingExecutable_ThrowsProcessStartFailed()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-exe-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<ProcessStartFailedException>(() => new ProcessLauncher().Start(missing, []));
    }

    [Fact]
    public void Start_NotExecutable_ThrowsProcessStartFailed()
    {
        if (OperatingSystem.IsWindows())
        {
            // The execute bit is a Unix concept; Windows decides executability by file type.
            return;
        }

        // GetTempFileName creates the file without the execute bit.
        var file = Path.GetTempFileName();
        try
        {
            Assert.Throws<ProcessStartFailedException>(() => new ProcessLauncher().Start(file, []));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Cancel_KilledProcess_ReportsExitCode137OnEveryPlatform()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"hang\":true}\n").ConfigureAwait(true);

        try
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
            using var cts = new CancellationTokenSource();
            await using var process = new ProcessLauncher().Start("dotnet", [FakeExePath], new ProcessOptions());

            cts.CancelAfter(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.WaitAsync(cts.Token)).ConfigureAwait(true);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline && process.ExitCode == -1)
            {
                await Task.Delay(50).ConfigureAwait(true);
            }

            Assert.Equal(137, process.ExitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }
}
