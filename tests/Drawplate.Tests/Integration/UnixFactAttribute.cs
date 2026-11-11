using System.Runtime.InteropServices;

using Xunit;

namespace Drawplate.Tests.Integration;

public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Skip = "This test requires Unix.";
    }
}
