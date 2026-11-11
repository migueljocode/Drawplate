using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Services.Conversion;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Integration;

[Trait("Tier", "T2")]
public sealed class RunnerIntegrationTests
{
    [FfmpegFact]
    public async Task Runner_Integration_ConvertsH264Yuv420pAac()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "input.mp4").ConfigureAwait(true);
            var output = Path.Combine(root, "out"); Directory.CreateDirectory(output);
            var runner = CreateRunner();
            var result = await runner.RunAsync(Path.Combine(root, "input.mp4"), output, [EncoderKind.Cpu], 50, SizeCap.None, true, null, null, null, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(FileResultStatus.Converted, result.Status);
            var probe = await MediaFixtures.Run(MediaFixtures.FfmpegPath, ["-hide_banner", "-i", result.Target], root).ConfigureAwait(true);
            Assert.Contains("Video: h264", probe.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("yuv420p", probe.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Audio: aac", probe.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    public async Task Runner_Integration_NoVideoStream_FailsWithoutOutput()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var input = Path.Combine(root, "audio.mp4");
            await MediaFixtures.Run(MediaFixtures.FfmpegPath, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-c:a", "aac", input], root).ConfigureAwait(true);
            var output = Path.Combine(root, "out"); Directory.CreateDirectory(output);
            var result = await CreateRunner().RunAsync(input, output, [EncoderKind.Cpu], 50, SizeCap.None, true, null, null, null, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(FileResultStatus.Failed, result.Status);
            Assert.Equal("no video stream", result.Reason);
            Assert.Empty(Directory.GetFiles(output));
        }
        finally { Directory.Delete(root, true); }
    }
    [FfmpegFact]
    public async Task Runner_Integration_Rerun_SkipsExisting()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "input.mp4").ConfigureAwait(true);
            var output = Path.Combine(root, "out"); Directory.CreateDirectory(output);
            var runner = CreateRunner();
            var first = await runner.RunAsync(Path.Combine(root, "input.mp4"), output, [EncoderKind.Cpu], 50, SizeCap.None, true, null, null, null, CancellationToken.None).ConfigureAwait(true);
            var second = await runner.RunAsync(Path.Combine(root, "input.mp4"), output, [EncoderKind.Cpu], 50, SizeCap.None, true, null, null, null, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(FileResultStatus.Converted, first.Status);
            Assert.Equal(FileResultStatus.Skipped, second.Status);
        }
        finally { Directory.Delete(root, true); }
    }

    private static ConversionRunner CreateRunner()
    {
        var launcher = new ProcessLauncher();
        return new ConversionRunner(MediaFixtures.FfmpegPath, launcher, new MediaProbe(launcher, MediaFixtures.FfmpegPath, null));
    }
}
