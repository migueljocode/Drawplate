using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Xunit;

namespace Drawplate.Tests.Architecture;

public class ArchitectureTests
{
    [Fact]
    public void Models_ReferencesOnlyFrameworkAssemblies()
    {
        var models = Assembly.Load("Drawplate.Models");
        foreach (var reference in models.GetReferencedAssemblies())
        {
            Assert.Matches(@"^(System|netstandard|mscorlib)", reference.Name!);
        }
    }

    [Fact]
    public void Models_DoesNotReferenceServicesOrConsole()
    {
        var models = Assembly.Load("Drawplate.Models");
        var names = models.GetReferencedAssemblies().Select(r => r.Name!).ToHashSet();
        Assert.DoesNotContain("Drawplate.Services", names);
        Assert.DoesNotContain("Drawplate.Console", names);
    }

    [Fact]
    public void Services_DoesNotReferenceSpectreConsole()
    {
        var services = Assembly.Load("Drawplate.Services");
        var names = services.GetReferencedAssemblies().Select(r => r.Name!).ToHashSet();
        Assert.DoesNotContain("Spectre.Console", names);
    }

    [Fact]
    public void Services_DoesNotReferenceConsole()
    {
        var services = Assembly.Load("Drawplate.Services");
        var names = services.GetReferencedAssemblies().Select(r => r.Name!).ToHashSet();
        Assert.DoesNotContain("Drawplate.Console", names);
    }

    [Fact]
    public void AssemblyReferenceHelper_SeesKnownReference()
    {
        var models = Assembly.Load("Drawplate.Models");
        Assert.NotNull(models);
    }
}
