using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

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

    [Fact]
    public void Only_ProcessLauncher_StartsProcesses()
    {
        var testDir = Path.GetDirectoryName(typeof(ArchitectureTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var launcher = Path.Combine("Processes", "ProcessLauncher.cs");
        var separator = Path.DirectorySeparatorChar;
        var startsProcess = new Regex(@"Process\.Start\(|new\s+(System\.Diagnostics\.)?ProcessStartInfo", RegexOptions.None, TimeSpan.FromSeconds(5));

        var offenders = Directory.EnumerateFiles(Path.Combine(repoDir, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !f.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !f.EndsWith(launcher, StringComparison.Ordinal))
            .Where(f => startsProcess.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(repoDir, f))
            .ToList();

        Assert.Empty(offenders);
    }
}
