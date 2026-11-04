using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

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
            var launcher = new ProcessLauncher();
            await using var process = launcher.Start("dotnet", [FakeExePath], new ProcessOptions(TimeSpan.FromMilliseconds(100)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var exit = await process.WaitAsync(cts.Token).ConfigureAwait(true);

            Assert.True(process.TimedOut);
            Assert.True(exit == -1 || exit == 0);
        }
        finally
        {
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
}
