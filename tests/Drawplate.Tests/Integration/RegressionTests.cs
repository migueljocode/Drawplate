using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Drawplate.Tests.Integration;

public sealed class RegressionTests
{
    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_FolderMode_OneBadFile_OthersContinue()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var names = new[] { "ok-landscape.mp4", "Space Name.MOV", "portrait.mkv", "tiny.webm", "odd.avi" };
            await MediaFixtures.Clip(root, names[0], 1280, 720).ConfigureAwait(true);
            await MediaFixtures.Clip(root, names[1], 1280, 720, true, "mpeg4", "mp2").ConfigureAwait(true);
            await MediaFixtures.Clip(root, names[2], 720, 1280).ConfigureAwait(true);
            await MediaFixtures.Clip(root, names[3], 160, 120, true, "libvpx", "libopus").ConfigureAwait(true);
            await MediaFixtures.Clip(root, names[4], 321, 241, true, "mpeg4", "mp2").ConfigureAwait(true);
            var expectedTime = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            foreach (var name in names)
            {
                File.SetLastWriteTimeUtc(Path.Combine(root, name), expectedTime);
            }
            await File.WriteAllBytesAsync(Path.Combine(root, "bad.mp4"), new byte[4096]).ConfigureAwait(true);
            await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "hi").ConfigureAwait(true);
            Directory.CreateDirectory(Path.Combine(root, "sub"));

            var timer = Stopwatch.StartNew();
            var result = await MediaFixtures.RunCli(root).ConfigureAwait(true);
            timer.Stop();

            var outputFiles = Directory.GetFiles(Path.Combine(root, "compressed"), "*.mp4");
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(5, outputFiles.Length);
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(60), $"Conversion took {timer.Elapsed}.");
            Assert.Contains("bad.mp4", result.Stdout + result.Stderr, StringComparison.Ordinal);

            foreach (var name in names)
            {
                var source = Path.Combine(root, name);
                var output = Path.Combine(root, "compressed", Path.GetFileNameWithoutExtension(name) + ".mp4");
                var probe = await MediaFixtures.Run(MediaFixtures.FfmpegPath, ["-hide_banner", "-i", output], root).ConfigureAwait(true);
                Assert.Contains("Video: h264", probe.Stderr, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("yuv420p", probe.Stderr, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Audio: aac", probe.Stderr, StringComparison.OrdinalIgnoreCase);
                var dimensions = Regex.Match(probe.Stderr, @"(?<width>\d{2,5})x(?<height>\d{2,5})");
                Assert.True(dimensions.Success, $"Could not read dimensions for {name}.");
                var width = int.Parse(dimensions.Groups["width"].Value, CultureInfo.InvariantCulture);
                var height = int.Parse(dimensions.Groups["height"].Value, CultureInfo.InvariantCulture);
                Assert.True(width % 2 == 0, $"{name} output dimensions {width}x{height} must have even width.");
                Assert.True(height % 2 == 0, $"{name} output dimensions {width}x{height} must have even height.");
                Assert.Equal(File.GetLastWriteTimeUtc(source), File.GetLastWriteTimeUtc(output));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_Rerun_SkipsExisting()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "rerun.mp4").ConfigureAwait(true);
            var first = await MediaFixtures.RunCli(root).ConfigureAwait(true);
            var target = Path.Combine(root, "compressed", "rerun.mp4");
            var beforeTime = File.GetLastWriteTimeUtc(target);
            var beforeLength = new FileInfo(target).Length;
            var second = await MediaFixtures.RunCli(root).ConfigureAwait(true);
            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Contains("output already exists", second.Stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeTime, File.GetLastWriteTimeUtc(target));
            Assert.Equal(beforeLength, new FileInfo(target).Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_SizeCap_Sd()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "wide.mp4", 1280, 720).ConfigureAwait(true);
            await MediaFixtures.Clip(root, "portrait.mp4", 720, 1280).ConfigureAwait(true);
            await MediaFixtures.Clip(root, "small.mp4", 160, 120).ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root, "-s", "sd").ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("852x480", await Probe(root, "wide.mp4").ConfigureAwait(true), StringComparison.Ordinal);
            Assert.Contains("480x852", await Probe(root, "portrait.mp4").ConfigureAwait(true), StringComparison.Ordinal);
            Assert.Contains("160x120", await Probe(root, "small.mp4").ConfigureAwait(true), StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(99, 2)]
    [InlineData(75, 14)]
    [InlineData(50, 26)]
    [InlineData(25, 39)]
    [InlineData(1, 51)]
    [Trait("Tier", "T0")]
    public void Reg_QualityMap_CrfTag(int quality, int expected) =>
        Assert.Equal(expected, Drawplate.Models.Encoding.QualityScale.ToCrf(quality));

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_Collision_FailsFast()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var x = Path.Combine(root, "x"); var y = Path.Combine(root, "y");
            Directory.CreateDirectory(x); Directory.CreateDirectory(y);
            await MediaFixtures.Clip(x, "clip.mp4").ConfigureAwait(true);
            File.Copy(Path.Combine(x, "clip.mp4"), Path.Combine(y, "clip.mov"));
            var result = await MediaFixtures.RunCli(root, Path.Combine(x, "clip.mp4"), Path.Combine(y, "clip.mov")).ConfigureAwait(true);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("More than one input would write the same", result.Stderr, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Reg_OutputEqualsInput_AllSkipped()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "already.mp4"), "source");
            var result = await MediaFixtures.RunCli(root, "-i", root, "-o", root).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("already in the output folder", result.Stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Reg_UnsupportedOnly_And_MissingFile()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "hi").ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root, Path.Combine(root, "missing.mp4"), Path.Combine(root, "notes.txt")).ConfigureAwait(true);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("File not found:", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("notes.txt", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("unsupported file type", result.Stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [UnixFact]
    [Trait("Tier", "T2")]
    public async Task Reg_Signals()
    {
        if (MediaFixtures.FindFfmpeg() is null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))) return;
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "signal.mp4", 1920, 1080).ConfigureAwait(true);
            var output = Path.Combine(root, "compressed");
            Directory.CreateDirectory(output);
            var partialCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher = new FileSystemWatcher(output, "*.partial.mp4");
            watcher.Created += (_, _) => partialCreated.TrySetResult();
            watcher.EnableRaisingEvents = true;
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = MediaFixtures.DrawplatePath,
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add("signal.mp4");
            Assert.True(process.Start());
            await partialCreated.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            using var signal = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/kill",
                UseShellExecute = false,
                ArgumentList = { "-TERM", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            }) ?? throw new InvalidOperationException("Could not start kill.");
            await signal.WaitForExitAsync().ConfigureAwait(true);
            Assert.Equal(0, signal.ExitCode);
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            Assert.Equal(130, process.ExitCode);
            Assert.Empty(Directory.GetFiles(output, "*.partial.mp4"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Reg_LeadingDash_AfterDoubleDash()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "source.mp4").ConfigureAwait(true);
            File.Copy(Path.Combine(root, "source.mp4"), Path.Combine(root, "-weird.mp4"));
            var result = await MediaFixtures.RunCli(root, "--", "-weird.mp4").ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(root, "compressed", "-weird.mp4")));
            Assert.DoesNotContain("Unknown option", result.Stderr, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Reg_HostileFilename_NoInjection()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "source.mp4").ConfigureAwait(true);
            var hostile = "q'quote $(touch PWNED) " + (char)96 + "id" + (char)96 + ".mp4";
            File.Copy(Path.Combine(root, "source.mp4"), Path.Combine(root, hostile));
            var result = await MediaFixtures.RunCli(root, "--", hostile).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.False(File.Exists(Path.Combine(root, "PWNED")));
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_AudioOnly_NoVideoStream()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Run(MediaFixtures.FfmpegPath,
                ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-c:a", "aac", Path.Combine(root, "audio.mp4")], root).ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root, "audio.mp4").ConfigureAwait(true);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("no video stream", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Tier", "T1")]
    public async Task Reg_OutputPathIsFile()
    {
        var root = MediaFixtures.Temp();
        try
        {
            var output = Path.Combine(root, "output"); await File.WriteAllTextAsync(output, "file");
            var result = await MediaFixtures.RunCli(root, "-o", output).ConfigureAwait(true);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Output path is a file, not a folder:", result.Stderr, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_FilesMode_DefaultOutput()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "single.mp4").ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root, Path.Combine(root, "single.mp4")).ConfigureAwait(true);
            Assert.Equal(0, result.ExitCode);
            Assert.True(Directory.Exists(Path.Combine(root, "compressed")));
        }
        finally { Directory.Delete(root, true); }
    }

    [FfmpegFact]
    [Trait("Tier", "T2")]
    public async Task Reg_Piped_NoAnsi()
    {
        var root = MediaFixtures.Temp();
        try
        {
            await MediaFixtures.Clip(root, "plain.mp4").ConfigureAwait(true);
            var result = await MediaFixtures.RunCli(root).ConfigureAwait(true);
            Assert.DoesNotContain('\u001b', result.Stdout + result.Stderr);
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task<string> Probe(string folder, string name)
    {
        var path = Path.Combine(folder, "compressed", Path.GetFileNameWithoutExtension(name) + ".mp4");
        var result = await MediaFixtures.Run(MediaFixtures.FfmpegPath, ["-hide_banner", "-i", path], folder).ConfigureAwait(true);
        return result.Stderr;
    }

}
