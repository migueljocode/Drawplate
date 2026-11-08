using System.IO;

using Drawplate.Console.Cli;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T0")]
public class HelpTextTests
{
    [Fact]
    public void HelpText_MatchesGolden()
    {
        var expected = File.ReadAllText(GoldenPath()).Replace("\r\n", "\n");
        Assert.Equal(expected, HelpText.Render());
    }

    [Fact]
    public void HelpText_EveryOptionListed()
    {
        var help = HelpText.Render();
        Assert.All(OptionTable.Entries, e => Assert.Contains(e.LongNames[0], help));
    }

    [Theory]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData(null, "0.0.0")]
    public void VersionInfo_StripsCommitSuffix(string? informational, string expected)
    {
        Assert.Equal(expected, VersionInfo.FromInformational(informational));
    }

    private static string GoldenPath()
    {
        var testDir = Path.GetDirectoryName(typeof(HelpTextTests).Assembly.Location)!;
        var repoDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoDir, "tests", "Drawplate.Tests", "Golden", "help.txt");
    }
}
