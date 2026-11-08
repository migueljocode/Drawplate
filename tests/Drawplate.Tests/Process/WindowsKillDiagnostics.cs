using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
public class WindowsKillDiagnostics
{
    [Fact]
    public async Task Diag_KillThenReadExitCode()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"hang\":true}\n").ConfigureAwait(true);
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
        try
        {
            var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add(FakeExeHarness.FakeExePath);
            using var p = global::System.Diagnostics.Process.Start(psi)!;
            await Task.Delay(500).ConfigureAwait(true);
            var before = p.HasExited;
            p.Kill(entireProcessTree: true);
            using var bound = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            var exited = true;
            try
            {
                await p.WaitForExitAsync(bound.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                exited = false;
            }

            var report = $"DIAG os={Environment.OSVersion.Platform} runningBefore={!before} waitForExit={exited} hasExited={p.HasExited} exitCode={(p.HasExited ? p.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : "n/a")}";
            throw new InvalidOperationException(report);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
        }
    }
}
