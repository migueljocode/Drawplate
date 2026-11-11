using System;
using System.Collections.Generic;
using System.IO;

using Drawplate.Console.Ui;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T0")]
public sealed class LiveProgressTests
{
    [Fact]
    public void Live_Piped_NoRedraw()
    {
        using var output = new StringWriter();
        var theme = Theme.Detect(false, new Dictionary<string, string?>(), 65001, 80);
        var live = new LiveProgress(output, theme, new TestTimeProvider());
        live.Draw(1, 1, "clip.mp4", 0.5, 1.8, TimeSpan.FromSeconds(12));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Live_Terminal_ThrottledTo10Hz()
    {
        using var output = new StringWriter();
        var theme = Theme.Detect(true, new Dictionary<string, string?> { ["TERM"] = "xterm" }, 65001, 80);
        var clock = new TestTimeProvider();
        var live = new LiveProgress(output, theme, clock);
        for (var i = 0; i < 20; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            live.Draw(1, 1, "clip.mp4", 0.5, 1.8, TimeSpan.FromSeconds(i));
        }

        Assert.Equal(10, Count(output.ToString(), "[1/1]"));
    }

    [Fact]
    public void Live_UnknownFraction_ShowsDashes()
    {
        using var output = new StringWriter();
        var theme = Theme.Detect(true, new Dictionary<string, string?> { ["TERM"] = "xterm" }, 437, 80);
        var live = new LiveProgress(output, theme, new TestTimeProvider());
        live.Draw(1, 1, "clip.mp4", null, null, TimeSpan.FromSeconds(12));
        Assert.Contains("--------------------  --%", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("eta", output.ToString(), StringComparison.Ordinal);
    }

    private static int Count(string value, string match)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(match, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += match.Length;
        }
        return count;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
