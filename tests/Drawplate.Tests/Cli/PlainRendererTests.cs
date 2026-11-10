using System;
using System.IO;
using System.Linq;

using Drawplate.Console.Ui;

using Xunit;

namespace Drawplate.Tests.Cli;

/// <summary>Golden text is written by hand from section D.4 of the plan.</summary>
[Trait("Tier", "T0")]
public class PlainRendererTests
{
    private static string Render(bool utf8, Action<IRenderer> act)
    {
        using var writer = new StringWriter();
        act(new PlainRenderer(writer, utf8));
        return writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void EverythingAscii(IRenderer r)
    {
        r.PlanCard("/in", "/in/compressed", 50, 26, "hd 1280x720", "[VAAPI] > [CPU]", 3, 128210145, true);
        r.Unsupported("notes.txt");
        r.FileFinished(1, 3, "a.mp4", true, "[CPU]", TimeSpan.FromSeconds(1), 1048576, 700000, null);
        r.FileFinished(2, 3, "bad.mp4", false, null, TimeSpan.Zero, 10, 0, "no video stream");
        r.FileSkipped(3, 3, "old.mp4", "output already exists");
        r.Summary(1, 1, 1, 2097152, 524288, "[CPU] x1", TimeSpan.FromSeconds(65), "/out");
        r.Note("Note: x");
    }

    [Fact]
    public void PlainRenderer_PlanCard_Golden()
    {
        var text = Render(true, r => r.PlanCard("/in", "/in/compressed", 50, 26, "hd 1280x720", "[VAAPI] > [CPU]", 3, 128210145, true));

        Assert.Equal(
            "Plan\n"
            + "Input     /in\n"
            + "Output    /in/compressed\n"
            + "Quality   50 (CRF 26)\n"
            + "Max size  hd 1280x720\n"
            + "Encoder   [VAAPI] > [CPU]\n"
            + "Files     3 video(s) · 122.3 MB · timestamps kept\n",
            text);
    }

    [Fact]
    public void PlainRenderer_PlanCard_NoCapTimestampsNotKept_Golden()
    {
        var text = Render(false, r => r.PlanCard("2 file(s)", "/o", 80, 11, null, "[CPU]", 2, 1024, false));

        Assert.Equal(
            "Plan\n"
            + "Input     2 file(s)\n"
            + "Output    /o\n"
            + "Quality   80 (CRF 11)\n"
            + "Max size  none\n"
            + "Encoder   [CPU]\n"
            + "Files     2 video(s) . 1.0 KB . timestamps not kept\n",
            text);
    }

    [Fact]
    public void PlainRenderer_FileLines_Golden()
    {
        var text = Render(true, r =>
        {
            r.Unsupported("notes.txt");
            r.FileFinished(1, 4, "a.mp4", true, "[CPU]", TimeSpan.FromSeconds(1), 1048576, 700000, null);
            r.FileFinished(2, 4, "b.mp4", true, "[VAAPI]", TimeSpan.FromSeconds(75), 1000, 1500, null);
            r.FileFinished(3, 4, "bad.mp4", false, null, TimeSpan.Zero, 10, 0, "no video stream");
            r.FileSkipped(4, 4, "old.mp4", "output already exists");
        });

        Assert.Equal(
            "– notes.txt (unsupported file type)\n"
            + "✓ [1/4] a.mp4  1.0 MB → 683.6 KB  ↓33%  [CPU]  0:01\n"
            + "✓ [2/4] b.mp4  1000 B → 1.5 KB  ↑50%  [VAAPI]  1:15\n"
            + "✗ [3/4] bad.mp4  no video stream\n"
            + "– [4/4] old.mp4  skipped: output already exists\n",
            text);
    }

    [Fact]
    public void PlainRenderer_FileLines_Ascii_Golden()
    {
        var text = Render(false, r =>
        {
            r.FileFinished(1, 2, "a.mp4", true, "[CPU]", TimeSpan.FromSeconds(1), 1048576, 700000, null);
            r.FileSkipped(2, 2, "old.mp4", "already in the output folder");
        });

        Assert.Equal(
            "+ [1/2] a.mp4  1.0 MB -> 683.6 KB  v33%  [CPU]  0:01\n"
            + "- [2/2] old.mp4  skipped: already in the output folder\n",
            text);
    }

    [Fact]
    public void PlainRenderer_Summary_Golden()
    {
        var text = Render(true, r => r.Summary(2, 1, 1, 2097152, 524288, "[VAAPI] x1 [CPU] x1", TimeSpan.FromSeconds(65), "/out"));

        Assert.Equal(
            "Summary\n"
            + "Result    2 converted   1 failed   1 skipped\n"
            + "Size      2.0 MB → 512.0 KB  ↓ 75% smaller\n"
            + "          ██████░░░░░░░░░░░░░░░░░░ 25% of original\n"
            + "Saved     1.5 MB\n"
            + "Encoders  [VAAPI] x1 [CPU] x1\n"
            + "Time      1:05\n"
            + "Output    /out\n"
            + "Finished with 1 failure(s).\n",
            text);
    }

    [Fact]
    public void PlainRenderer_Summary_AllOk_Larger_Golden()
    {
        var text = Render(false, r => r.Summary(1, 0, 0, 1000, 1200, "[CPU] x1", TimeSpan.FromSeconds(5), "/out"));

        Assert.Equal(
            "Summary\n"
            + "Result    1 converted\n"
            + "Size      1000 B -> 1.2 KB  ^ 20% larger\n"
            + "          ######################## 120% of original\n"
            + "Encoders  [CPU] x1\n"
            + "Time      0:05\n"
            + "Output    /out\n"
            + "All done.\n",
            text);
    }

    [Fact]
    public void PlainRenderer_Summary_NothingConverted_OmitsSizeRows()
    {
        var text = Render(true, r => r.Summary(0, 0, 2, 0, 0, string.Empty, TimeSpan.Zero, "/out"));

        Assert.Equal(
            "Summary\n"
            + "Result    0 converted   2 skipped\n"
            + "Time      0:00\n"
            + "Output    /out\n"
            + "All done.\n",
            text);
    }

    [Fact]
    public void PlainRenderer_AsciiFallback_NoNonAscii()
    {
        var text = Render(false, EverythingAscii);

        Assert.All(text, c => Assert.True(c < 0x80, $"non-ASCII character U+{(int)c:X4}"));
    }

    [Fact]
    public void PlainRenderer_NoAnsiEver()
    {
        var hostile = "evil\u001b[31m.mp4";
        foreach (var utf8 in new[] { true, false })
        {
            var text = Render(utf8, r =>
            {
                r.Unsupported(hostile);
                r.FileFinished(1, 1, hostile, true, "[CPU]", TimeSpan.Zero, 10, 5, null);
                r.FileFinished(1, 1, hostile, false, null, TimeSpan.Zero, 10, 0, "encoder failed: " + hostile);
                r.FileSkipped(1, 1, hostile, "output already exists");
            });

            Assert.DoesNotContain('\u001b', text);
            Assert.True(text.Contains("evil[31m.mp4", StringComparison.Ordinal));
        }
    }
}
