using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Console.Cli;
using Drawplate.Console.Signals;
using Drawplate.Console.Ui;
using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Models.Planning;
using Drawplate.Models.Setup;
using Drawplate.Services.Conversion;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;

namespace Drawplate.Console;

internal static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using var signalHandler = new SignalHandler();

        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-V"))
        {
            global::System.Console.WriteLine("drawplate 0.0.0");
            return 0;
        }

        var parsed = ArgParser.Parse(args);
        if (parsed.Error is { } err)
        {
            global::System.Console.Error.WriteLine(err.Text);
            if (err.Hint is { } h) global::System.Console.Error.WriteLine($"  {h}");
            return err.ExitCode;
        }

        if (parsed.Kind == CommandKind.Version)
        {
            global::System.Console.WriteLine("drawplate 0.0.0");
            return 0;
        }

        if (parsed.Kind == CommandKind.Help)
        {
            global::System.Console.WriteLine("Usage: drawplate [options] [file ...]");
            return 0;
        }

        if (parsed.Kind is CommandKind.Setup or CommandKind.Doctor)
        {
            global::System.Console.Error.WriteLine(Messages.E_TEMP_01().Text);
            return 2;
        }

        if (parsed.Options is { } o && (o.InstallDeps || o.Interactive || o.NoInstall))
        {
            global::System.Console.Error.WriteLine(Messages.E_TEMP_01().Text);
            return 2;
        }

        var options = parsed.Options!;

        var validation = OptionValidator.Validate(options, OperatingSystemKind.Linux);
        if (validation.Error is { } verr)
        {
            global::System.Console.Error.WriteLine(validation.Error.Text);
            if (validation.Error.Hint is { } vh) global::System.Console.Error.WriteLine($"  {vh}");
            return verr.ExitCode;
        }

        var platform = PlatformDetector.Detect();
        var candidate = FfmpegLocator.Locate(
            options.FfmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [],
            File.Exists,
            Directory.GetDirectories,
            platform.Os == OperatingSystemKind.Linux
                ? ["/opt/homebrew/bin", "/usr/local/bin", "/home/linuxbrew/.linuxbrew/bin", "/usr/bin"]
                : []);

        if (candidate is null)
        {
            var suggestions = InstallAdvisor.Suggestions(platform);
            global::System.Console.Error.WriteLine(InstallAdvisor.NotFoundText(platform, suggestions));
            return 3;
        }

        var infoService = new FfmpegInfoService(new ProcessLauncher());
        var info = infoService.GetInfoAsync(candidate.Path, CancellationToken.None).GetAwaiter().GetResult();
        if (info is null)
        {
            global::System.Console.Error.WriteLine(Messages.E_FF_02(candidate.Path, "unknown"));
            return 3;
        }

        var probe = new EncoderProbe(new ProcessLauncher(), infoService, platform.Os);
        var results = probe.ProbeAllAsync(info, CancellationToken.None).GetAwaiter().GetResult();

        if (options.ListEncoders)
        {
            foreach (var r in results)
            {
                if (r.Passed) global::System.Console.WriteLine(EncoderSets.Id(r.Kind!));
            }
            return 0;
        }

        var chain = results.Where(r => r.Passed).Select(r => r.Kind!).ToImmutableArray();
        if (chain.IsEmpty)
        {
            global::System.Console.Error.WriteLine(Messages.E_FF_03());
            return 3;
        }

        var inputFolder = options.InputFolder ?? ".";
        var outputFolder = options.Files.Length > 0
            ? Path.Combine(Directory.GetCurrentDirectory(), "compressed")
            : Path.Combine(inputFolder, "compressed");

        if (options.Files.Length > 0 && !string.IsNullOrEmpty(options.InputFolder))
        {
            global::System.Console.Error.WriteLine(Messages.E_USAGE_11());
            return 2;
        }

        var fs = new RealPlanFileSystem();
        var planSources = options.Files.Length > 0 ? options.Files : ImmutableArray.Create(inputFolder);
        var plan = ConversionPlanner.Build(planSources, outputFolder, fs, platform.Os);

        if (plan.Collisions.Length > 0)
        {
            global::System.Console.Error.WriteLine(Messages.E_USAGE_09(plan.Collisions));
            return 2;
        }

        var convertFiles = plan.Files.Where(f => f.Status == PlanStatus.Convert).ToImmutableArray();
        var skipCount = plan.Files.Length - convertFiles.Length;

        if (convertFiles.Length == 0 && skipCount == 0)
        {
            global::System.Console.Error.WriteLine(Messages.E_RUN_01());
            global::System.Console.Error.WriteLine("  Supported formats: mov, mp4, mkv, avi, webm.");
            return 1;
        }

        Directory.CreateDirectory(outputFolder);

        var totalInputSize = 0L;
        foreach (var f in convertFiles)
        {
            totalInputSize += new FileInfo(f.Source).Length;
        }

        var renderer = new PlainRenderer(global::System.Console.OutputEncoding.CodePage == 65001);
        renderer.PlanCard(
            inputFolder,
            outputFolder,
            int.Parse(options.Quality ?? "50"),
            null,
            options.MaxSize,
            "[CPU]",
            convertFiles.Length,
            totalInputSize,
            !options.NoTimestamps);

        var runner = new ConversionRunner(candidate.Path, new ProcessLauncher(), new MediaProbe(new ProcessLauncher()));
        int converted = 0, failed = 0;
        var fileArray = plan.Files.ToArray();
        for (int fileIdx = 0; fileIdx < fileArray.Length; fileIdx++)
        {
            var f = fileArray[fileIdx];
            if (f.Status != PlanStatus.Convert) continue;

            var result = runner.RunAsync(f.Source, outputFolder, chain, int.Parse(options.Quality ?? "50"), SizeCap.None, options.NoTimestamps, null, CancellationToken.None).GetAwaiter().GetResult();

            if (result.Status == FileResultStatus.Converted) converted++;
            else if (result.Status == FileResultStatus.Failed) failed++;

            var glyph = result.Status == FileResultStatus.Converted ? "✓" : "✗";
            global::System.Console.WriteLine($"{glyph} [{fileIdx + 1}/{plan.Files.Length}] {Path.GetFileName(f.Source)}");
        }

        renderer.Summary(converted, failed, skipCount, totalInputSize, 0, "[CPU] x1", TimeSpan.Zero, outputFolder);

        return failed > 0 ? 1 : 0;
    }
}
