using System.Collections.Generic;
using System.IO;

namespace Drawplate.Tests.Process;

public static class FakeExeHarness
{
    public static string FakeExePath =>
        Path.Combine(GetRepoDir(), "tests", "FakeExe", "bin", "Release", "net10.0", "FakeExe.dll");

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
