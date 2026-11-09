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

    /// <summary>
    /// An executable that runs FakeExe: a shell script on Unix, a .cmd file on Windows. The caller deletes it.
    /// </summary>
    public static string CreateWrapper()
    {
        var wrapper = Path.GetTempFileName();
        File.Delete(wrapper);
        if (OperatingSystem.IsWindows())
        {
            wrapper = Path.ChangeExtension(wrapper, ".cmd");
            File.WriteAllText(wrapper, $"@echo off\r\ndotnet \"{FakeExePath}\" %*\r\n");
        }
        else
        {
            File.WriteAllText(wrapper, $"#!/bin/sh\nexec dotnet \"{FakeExePath}\" \"$@\"\n");
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return wrapper;
    }

    public static string WriteScript(IEnumerable<string> actions)
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, actions);
        return path;
    }
}
