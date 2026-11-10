using System;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>A fact that runs only where POSIX signals and tools such as kill and pgrep exist.</summary>
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
