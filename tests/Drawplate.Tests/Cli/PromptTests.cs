using System.IO;

using Drawplate.Services.Interaction;

using Xunit;

namespace Drawplate.Tests.Cli;

public class PromptTests
{
    [Fact]
    public void Confirm_Empty_UsesDefault()
    {
        var input = new StringReader("\n");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.True(prompt.Confirm("Run it now?", true));
        Assert.False(prompt.Confirm("Run it now?", false));
    }

    [Fact]
    public void Confirm_Yes_No_Variants()
    {
        var input = new StringReader("y\nYES\nn\nNo\n");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.True(prompt.Confirm("Q?", true));
        Assert.True(prompt.Confirm("Q?", false));
        Assert.False(prompt.Confirm("Q?", true));
        Assert.False(prompt.Confirm("Q?", false));
    }

    [Fact]
    public void Confirm_Invalid_ThreeTimes_ReturnsFalse()
    {
        var input = new StringReader("x\nq\nz\n");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.False(prompt.Confirm("Q?", true));
    }

    [Fact]
    public void Confirm_Eof_ReturnsDefault()
    {
        var input = new StringReader("");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.True(prompt.Confirm("Q?", true));
    }

    [Fact]
    public void Ask_Empty_ReturnsCurrent()
    {
        var input = new StringReader("\n");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.Equal("current", prompt.Ask("Encoder", "current"));
    }

    [Fact]
    public void Ask_Provided_ReturnsValue()
    {
        var input = new StringReader("nvenc\n");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, true, true);
        Assert.Equal("nvenc", prompt.Ask("Encoder", "auto"));
    }

    [Fact]
    public void IsInteractive_FalseWhenRedirected()
    {
        var input = new StringReader("");
        var output = new StringWriter();
        var prompt = new ConsolePrompt(input, output, false, true);
        Assert.False(prompt.IsInteractive);
        prompt = new ConsolePrompt(input, output, true, false);
        Assert.False(prompt.IsInteractive);
        prompt = new ConsolePrompt(input, output, true, true);
        Assert.True(prompt.IsInteractive);
    }
}
