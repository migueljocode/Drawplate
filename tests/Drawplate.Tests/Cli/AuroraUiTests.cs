using System;
using System.Collections.Immutable;
using System.IO;

using Drawplate.Console.Cli;
using Drawplate.Console.Ui;
using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;
using Drawplate.Services.Setup;

using Xunit;

namespace Drawplate.Tests.Cli;

[Trait("Tier", "T0")]
public sealed class AuroraUiTests
{
    [Fact]
    public void Gradient_Sample_EndpointsMatchAuroraPalette()
    {
        Assert.Equal(Palette.Aurora[0], Gradient.Sample(Palette.Aurora, 0.0));
        Assert.Equal(Palette.Aurora[^1], Gradient.Sample(Palette.Aurora, 1.0));
        Assert.Equal(Palette.Aurora[0], Gradient.Sample(Palette.Aurora, double.NaN));
    }

    [Theory]
    [InlineData(20, 0.0, true)]
    [InlineData(20, 0.37, true)]
    [InlineData(20, 1.0, true)]
    [InlineData(24, 0.62, false)]
    public void Gradient_Bar_HasExactVisibleWidth(int width, double fraction, bool utf8)
    {
        var bar = Gradient.Bar(width, fraction, Palette.Aurora, frame: 3, utf8: utf8);
        Assert.Equal(width, AnsiText.VisibleLength(bar));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gradient_IndeterminateBar_HasExactVisibleWidth(bool utf8)
    {
        for (var frame = 0; frame < 30; frame++)
        {
            var bar = Gradient.IndeterminateBar(22, frame, utf8);
            Assert.Equal(22, AnsiText.VisibleLength(bar));
        }
    }

    [Fact]
    public void Gradient_Glide_ApproachesTargetSmoothly()
    {
        var next = Gradient.Glide(0.2, 0.8, 0.1, rate: 8.0);
        Assert.InRange(next, 0.45, 0.65);
        Assert.Equal(0.2, Gradient.Glide(0.2, 0.8, 0.0));
    }

    [Fact]
    public void AnsiText_ClipVisible_RespectsMaxColumnsAndResetsEscape()
    {
        var colored = $"{Palette.Cyan.Fg()}Hello{Rgb.Reset} {Palette.Emerald.Fg()}DrawplateWorld{Rgb.Reset}";
        var clipped = AnsiText.ClipVisible(colored, 10);
        Assert.Equal(10, AnsiText.VisibleLength(clipped));
        Assert.Equal("Hello Draw", AnsiText.Strip(clipped));
        Assert.EndsWith(Rgb.Reset, clipped, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(36, true)]
    [InlineData(60, true)]
    [InlineData(100, true)]
    [InlineData(36, false)]
    [InlineData(65, false)]
    [InlineData(100, false)]
    public void BrandLogo_AdaptsToWidthWithoutOverflow(int width, bool utf8)
    {
        var lines = BrandLogo.RenderLines(width, utf8, frame: 4, reveal: 0.8);
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.True(AnsiText.VisibleLength(line) < width));
    }

    [Theory]
    [InlineData(45, true)]
    [InlineData(80, true)]
    [InlineData(80, false)]
    public void PanelBuilder_ClampsLinesBelowTerminalWidth(int width, bool utf8)
    {
        var panel = PanelBuilder.Build(
            "CONVERSION PLAN",
            ["Input   /very/long/path/that/could/overflow/on/narrow/windows/terminals/video.mp4"],
            width,
            utf8);
        Assert.All(panel, line => Assert.True(AnsiText.VisibleLength(line) < width));
    }

    [Theory]
    [InlineData(52, true)]
    [InlineData(90, true)]
    [InlineData(90, false)]
    public void HelpView_ListsEveryOptionAndNeverOverflowsWidth(int width, bool utf8)
    {
        var lines = HelpView.BuildBodyLines(width, utf8);
        var plain = string.Join('\n', System.Linq.Enumerable.Select(lines, AnsiText.Strip));
        Assert.All(OptionTable.Entries, entry => Assert.Contains(entry.LongNames[0], plain, StringComparison.Ordinal));
        Assert.All(lines, line => Assert.True(AnsiText.VisibleLength(line) < width));
    }

    [Fact]
    public void RichRenderer_SanitizesHostileFilename_And_RendersSummary()
    {
        using var sw = new StringWriter();
        var theme = new Theme(Colors: true, Utf8Glyphs: true, Width: 90, IsInteractive: false, IsTerminal: true, CanRedraw: true);
        var renderer = new RichRenderer(sw, theme);
        renderer.PlanCard("/in", "/out", 50, 26, "hd 1280x720", "[VAAPI] > [CPU]", 1, 2048, true);
        renderer.FileFinished(1, 1, "evil\u001b[31m.mp4", true, "[CPU]", TimeSpan.FromSeconds(2), 2048, 1024, null);
        renderer.Summary(1, 0, 0, 2048, 1024, "[CPU] x1", TimeSpan.FromSeconds(2), "/out");

        var plain = AnsiText.Strip(sw.ToString());
        Assert.Contains("CONVERSION PLAN", plain, StringComparison.Ordinal);
        Assert.Contains("evil[31m.mp4", plain, StringComparison.Ordinal);
        Assert.Contains("SUMMARY", plain, StringComparison.Ordinal);
        Assert.Contains("50% of original", plain, StringComparison.Ordinal);
        Assert.Contains("All done.", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void RichStatusView_Doctor_FormatsAllStatusesWithinWidth()
    {
        using var sw = new StringWriter();
        var theme = new Theme(Colors: true, Utf8Glyphs: true, Width: 80, IsInteractive: true, IsTerminal: true, CanRedraw: true);
        var platform = new PlatformInfo(OperatingSystemKind.Linux, "ubuntu", ImmutableHashSet<string>.Empty, "ubuntu", null, null);
        var report = new DoctorReport
        {
            Lines =
            [
                new DoctorLine("ok", "FFmpeg", "/usr/local/bin/ffmpeg (8.0)"),
                new DoctorLine("note", "Hardware", "no GPU encoder available"),
                new DoctorLine("outdated", "FFmpeg", "5.1.1"),
            ],
            ExitCode = 0,
        };

        RichStatusView.WriteDoctor(sw, report, platform, theme);
        var plain = AnsiText.Strip(sw.ToString());
        Assert.Contains("DRAWPLATE DOCTOR", plain, StringComparison.Ordinal);
        Assert.Contains("FFmpeg", plain, StringComparison.Ordinal);
        Assert.Contains("Hardware", plain, StringComparison.Ordinal);
    }
}
