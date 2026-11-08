using System;
using System.Collections.Generic;
using System.IO;

namespace Drawplate.Tests.Process;

public static class FakeExeHarness
{
    private static readonly string[] Configurations = ["Release", "Debug"];

    public static string FakeExePath { get; } = ResolveFakeExe();

    private static string ResolveFakeExe()
    {
        var root = Path.Combine(GetRepoDir(), "tests", "FakeExe", "bin");
        foreach (var configuration in Configurations)
        {
            var candidate = Path.Combine(root, configuration, "net10.0", "FakeExe.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Build tests/FakeExe first");
    }

    private static string GetRepoDir()
    {
        var testDir = Path.GetDirectoryName(typeof(FakeExeHarness).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
    }

    public static string WriteScript(IEnumerable<string> actions)
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, actions);
        return path;
    }
}
