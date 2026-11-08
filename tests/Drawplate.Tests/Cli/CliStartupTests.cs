using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T1")]
public class CliStartupTests
{
    public static IEnumerable<object[]> NoEnvironmentFlags() =>
    [
        ["-h"],
        ["--help"],
        ["-V"],
        ["--version"],
    ];

    [Theory]
    [MemberData(nameof(NoEnvironmentFlags))]
    public void Cli_Help_ExitsZero_NoEnvironment(string flag)
    {
        var workDir = CreateTempDir();
        try
        {
            var psi = StartInfo(DrawplateExe(), flag);
            psi.WorkingDirectory = workDir;
            psi.Environment["PATH"] = string.Empty;
            psi.Environment["DRAWPLATE_FFMPEG"] = Path.Combine(workDir, "missing", "ffmpeg");

            var (exitCode, stdout, _) = Run(psi);

            Assert.Equal(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stdout));
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public void Cli_Help_StartsNoProcess()
    {
        var tempDir = CreateTempDir();
        try
        {
            var binDir = Path.Combine(tempDir, "bin");
            Directory.CreateDirectory(binDir);
            CopyFakeExeAs("ffmpeg", binDir);
            var log = Path.Combine(tempDir, "fake.log");

            var psi = StartInfo(DrawplateExe(), "-h");
            psi.WorkingDirectory = tempDir;
            psi.Environment["PATH"] = binDir;
            psi.Environment["FAKE_EXE_LOG"] = log;

            var (exitCode, _, _) = Run(psi);

            Assert.Equal(0, exitCode);
            Assert.False(File.Exists(log));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Cli_UnknownOption_ExitsTwo()
    {
        var psi = StartInfo(DrawplateExe(), "--bogus");

        var (exitCode, stdout, stderr) = Run(psi);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("Unknown option '--bogus'.", stderr);
    }

    [Fact]
    public void AppHost_RunGuarded_UnexpectedException_ReturnsOneNoStackTrace()
    {
        var original = global::System.Console.Error;
        using var captured = new StringWriter();
        global::System.Console.SetError(captured);
        int exitCode;
        try
        {
            exitCode = Drawplate.Console.AppHost.RunGuarded(() => throw new InvalidOperationException("boom"));
        }
        finally
        {
            global::System.Console.SetError(original);
        }

        var text = captured.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("Unexpected error: boom", text);
        Assert.DoesNotContain("   at ", text);
    }

    [Fact]
    public void AppHost_RunGuarded_Cancelled_Returns130()
    {
        var original = global::System.Console.Error;
        using var captured = new StringWriter();
        global::System.Console.SetError(captured);
        int exitCode;
        try
        {
            exitCode = Drawplate.Console.AppHost.RunGuarded(() => throw new OperationCanceledException());
        }
        finally
        {
            global::System.Console.SetError(original);
        }

        Assert.Equal(130, exitCode);
        Assert.Contains("Interrupted.", captured.ToString());
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "drawplate-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ProcessStartInfo StartInfo(string executable, string argument)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(argument);
        return psi;
    }

    private static (int ExitCode, string StdOut, string StdErr) Run(ProcessStartInfo psi)
    {
        using var process = global::System.Diagnostics.Process.Start(psi)!;
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = stderrTask.GetAwaiter().GetResult();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private static string RepoDir()
    {
        var testDir = Path.GetDirectoryName(typeof(CliStartupTests).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
    }

    private static string DrawplateExe()
    {
        var tfmDir = Path.GetDirectoryName(typeof(CliStartupTests).Assembly.Location)!;
        var configuration = Path.GetFileName(Path.GetDirectoryName(tfmDir)!);
        var name = OperatingSystem.IsWindows() ? "drawplate.exe" : "drawplate";
        var path = Path.Combine(RepoDir(), "src", "Drawplate.Console", "bin", configuration, "net10.0", name);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Build src/Drawplate.Console first");
        }
        return path;
    }

    /// <summary>
    /// Copies FakeExe into <paramref name="directory"/>: the runtime files keep their names (the apphost
    /// finds FakeExe.dll by the name it was built with) and the apphost is copied under <paramref name="name"/>.
    /// </summary>
    private static void CopyFakeExeAs(string name, string directory)
    {
        var fakeDir = Path.GetDirectoryName(FakeExeHarness.FakeExePath)!;
        foreach (var file in new[] { "FakeExe.dll", "FakeExe.runtimeconfig.json", "FakeExe.deps.json" })
        {
            File.Copy(Path.Combine(fakeDir, file), Path.Combine(directory, file));
        }

        var host = OperatingSystem.IsWindows() ? "FakeExe.exe" : "FakeExe";
        var target = OperatingSystem.IsWindows() ? name + ".exe" : name;
        File.Copy(Path.Combine(fakeDir, host), Path.Combine(directory, target));
    }
}
