using System;
using System.Diagnostics;
using System.IO;

using Drawplate.Models.Cli;

using Xunit;

namespace Drawplate.Tests.Cli;

public class VersionTests
{
    private static readonly string ExeName = OperatingSystem.IsWindows() ? "drawplate.exe" : "drawplate";

    [Theory]
    [InlineData("-V")]
    [InlineData("--version")]
    public void Version_Flag_PrintsVersionAndExitsZero(string flag)
    {
        var exe = GetDrawplateExe();
        using var process = CliProcess.Start(exe, [flag]);
        process.WaitForExit();
        Assert.Equal((int)ExitCode.Ok, process.ExitCode);
        var output = process.StandardOutput.ReadToEnd();
        Assert.StartsWith("drawplate ", output);
    }

    [Fact]
    public void UnknownOption_ExitsTwo()
    {
        var exe = GetDrawplateExe();
        using var process = CliProcess.Start(exe, ["--bogus"]);
        process.WaitForExit();
        Assert.Equal((int)ExitCode.UsageError, process.ExitCode);
    }

    private static string GetDrawplateExe()
    {
        var testDir = Path.GetDirectoryName(typeof(VersionTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoDir, "src", "Drawplate.Console", "bin", "Release", "net10.0", ExeName);
        if (!File.Exists(exe))
        {
            exe = Path.Combine(repoDir, "src", "Drawplate.Console", "bin", "Debug", "net10.0", ExeName);
        }
        return exe;
    }
}
