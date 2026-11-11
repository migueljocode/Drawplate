using System.Collections.Generic;

using Drawplate.Console.Ui;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T0")]
public sealed class ThemeTests
{
    [Fact]
    public void Theme_NoColor_Plain()
    {
        var theme = Theme.Detect(true, new Dictionary<string, string?> { ["NO_COLOR"] = "1", ["TERM"] = "xterm" }, 65001, 120);
        Assert.False(theme.Colors);
        Assert.True(theme.Utf8Glyphs);
    }

    [Fact]
    public void Theme_Dumb_Ascii()
    {
        var theme = Theme.Detect(true, new Dictionary<string, string?> { ["TERM"] = "dumb" }, 65001, null);
        Assert.False(theme.Colors);
        Assert.False(theme.Utf8Glyphs);
        Assert.False(theme.CanRedraw);
        Assert.Equal(80, theme.Width);
    }

    [Fact]
    public void Theme_Utf8_Glyphs()
    {
        var theme = Theme.Detect(true, new Dictionary<string, string?> { ["TERM"] = "xterm-256color" }, 65001, 100);
        Assert.True(theme.Colors);
        Assert.True(theme.Utf8Glyphs);
        Assert.True(theme.IsInteractive);
    }
}
