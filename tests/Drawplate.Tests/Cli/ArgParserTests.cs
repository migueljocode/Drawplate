using System.Collections.Generic;
using System.Linq;

using Drawplate.Console.Cli;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T0")]
public class ArgParserTests
{
    public static IEnumerable<object[]> ShortLetters() =>
        OptionTable.Entries.Where(e => e.ShortName.Length > 0).Select(e => new object[] { e.ShortName });

    public static IEnumerable<object[]> LongNames() =>
        OptionTable.Entries.SelectMany(e => e.LongNames).Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(ShortLetters))]
    public void Short_AllLetters_Resolve(string letter)
    {
        var entry = OptionTable.Entries.Single(e => e.ShortName == letter);
        var args = entry.TakesValue ? new[] { "-" + letter, "value" } : new[] { "-" + letter };
        Assert.NotEqual(CommandKind.Error, ArgParser.Parse(args).Kind);
    }

    [Theory]
    [MemberData(nameof(LongNames))]
    public void Long_AllNames_Resolve(string name)
    {
        var entry = OptionTable.Entries.Single(e => e.LongNames.Contains(name));
        var args = entry.TakesValue ? new[] { name, "value" } : new[] { name };
        Assert.NotEqual(CommandKind.Error, ArgParser.Parse(args).Kind);
    }

    [Fact]
    public void ShortName_HasNoDash()
    {
        Assert.All(OptionTable.Entries, e => Assert.DoesNotContain("-", e.ShortName));
    }

    [Fact]
    public void Short_H_ReturnsHelp()
    {
        Assert.Equal(CommandKind.Help, ArgParser.Parse(["-h"]).Kind);
    }

    [Fact]
    public void Long_Help_ReturnsHelp()
    {
        Assert.Equal(CommandKind.Help, ArgParser.Parse(["--help"]).Kind);
    }

    [Fact]
    public void Short_V_ReturnsVersion()
    {
        Assert.Equal(CommandKind.Version, ArgParser.Parse(["-V"]).Kind);
    }

    [Fact]
    public void Version_AfterFlag_ReturnsVersion()
    {
        Assert.Equal(CommandKind.Version, ArgParser.Parse(["-t", "-V"]).Kind);
    }

    [Fact]
    public void Version_BeforeBadArg_ReturnsVersion()
    {
        Assert.Equal(CommandKind.Version, ArgParser.Parse(["-V", "--bogus"]).Kind);
    }

    [Fact]
    public void BadArg_BeforeVersion_ReturnsError()
    {
        var parsed = ArgParser.Parse(["--bogus", "-V"]);
        Assert.Equal(CommandKind.Error, parsed.Kind);
        Assert.Equal("E-USAGE-01", parsed.Error!.Id);
    }

    [Fact]
    public void Help_WithFiles_ReturnsHelp()
    {
        Assert.Equal(CommandKind.Help, ArgParser.Parse(["a.mp4", "-h"]).Kind);
    }

    [Fact]
    public void Cluster_FlagsAndAttachedValue_Parse()
    {
        var options = ArgParser.Parse(["-tcq50"]).Options!;
        Assert.True(options.NoTimestamps);
        Assert.True(options.Cpu);
        Assert.Equal("50", options.Quality);
    }

    [Theory]
    [InlineData("-q", "50")]
    [InlineData("--quality", "50")]
    public void Value_NextArgument_Parses(string flag, string value)
    {
        Assert.Equal(value, ArgParser.Parse([flag, value]).Options!.Quality);
    }

    [Fact]
    public void Value_Missing_E_USAGE_02()
    {
        var parsed = ArgParser.Parse(["-q"]);
        Assert.Equal(CommandKind.Error, parsed.Kind);
        Assert.Equal("E-USAGE-02", parsed.Error!.Id);
    }

    [Fact]
    public void Output_Short_StoredInOutputFolder()
    {
        Assert.Equal("out", ArgParser.Parse(["-o", "out"]).Options!.OutputFolder);
    }

    [Fact]
    public void Output_Long_Equals_Form()
    {
        Assert.Equal("out", ArgParser.Parse(["--output=out"]).Options!.OutputFolder);
    }

    [Fact]
    public void Long_FlagWithValue_E_USAGE_03()
    {
        var parsed = ArgParser.Parse(["--cpu=1"]);
        Assert.Equal("E-USAGE-03", parsed.Error!.Id);
    }

    [Fact]
    public void DoubleDash_RestAreFiles()
    {
        Assert.Equal<string[]>(["-x.mp4"], ArgParser.Parse(["--", "-x.mp4"]).Files.ToArray());
    }

    [Fact]
    public void Setup_First_ReturnsSetup()
    {
        var parsed = ArgParser.Parse(["setup", "-y"]);
        Assert.Equal(CommandKind.Setup, parsed.Kind);
        Assert.True(parsed.Options!.InstallDeps);
    }

    [Fact]
    public void Setup_WithFile_E_USAGE_01()
    {
        Assert.Equal("E-USAGE-01", ArgParser.Parse(["setup", "a.mp4"]).Error!.Id);
    }

    [Fact]
    public void Setup_NotFirst_IsFile()
    {
        var parsed = ArgParser.Parse(["a.mp4", "setup"]);
        Assert.Equal(CommandKind.Convert, parsed.Kind);
        Assert.Equal<string[]>(["a.mp4", "setup"], parsed.Files.ToArray());
    }

    [Fact]
    public void DotSlashSetup_IsFile()
    {
        Assert.Equal<string[]>(["./setup"], ArgParser.Parse(["./setup"]).Files.ToArray());
    }

    [Fact]
    public void DoubleDashSetup_IsFile()
    {
        Assert.Equal<string[]>(["setup"], ArgParser.Parse(["--", "setup"]).Files.ToArray());
    }

    [Fact]
    public void Doctor_First_ReturnsDoctor()
    {
        Assert.Equal(CommandKind.Doctor, ArgParser.Parse(["doctor"]).Kind);
    }

    [Fact]
    public void Unknown_Short_E_USAGE_01()
    {
        Assert.Equal("E-USAGE-01", ArgParser.Parse(["-z"]).Error!.Id);
    }

    [Fact]
    public void Unknown_Long_E_USAGE_01()
    {
        Assert.Equal("E-USAGE-01", ArgParser.Parse(["--bogus"]).Error!.Id);
    }
}
