using System.Collections.Generic;
using System.Diagnostics;

namespace Drawplate.Tests.Cli;

internal static class CliProcess
{
    public static global::System.Diagnostics.Process Start(string executable, IReadOnlyList<string> arguments)
    {
        System.ArgumentNullException.ThrowIfNull(arguments);
        System.ArgumentNullException.ThrowIfNull(executable);
        var psi = new global::System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        return global::System.Diagnostics.Process.Start(psi)!;
    }
}
