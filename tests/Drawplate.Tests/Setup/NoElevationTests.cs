using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace Drawplate.Tests.Setup;

public class NoElevationTests
{
    [Fact]
    [Trait("Tier", "T4")]
    public void Source_DoesNotInvokeElevationCommands()
    {
        var testDirectory = Path.GetDirectoryName(typeof(NoElevationTests).Assembly.Location)!;
        var repositoryDirectory = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", ".."));
        var forbidden = new Regex(@"\b(sudo|pkexec|runas|Verb)\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

        var offenders = Directory.EnumerateFiles(Path.Combine(repositoryDirectory, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.EndsWith(Path.Combine("Setup", "InstallAdvisor.cs"), StringComparison.Ordinal))
            .Where(path => forbidden.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryDirectory, path))
            .ToArray();

        Assert.Empty(offenders);
    }
}
