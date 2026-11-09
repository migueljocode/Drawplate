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

internal static class AppHost
{
    /// <summary>The only place that turns exceptions into exit codes (C.4).</summary>
    internal static int RunGuarded(Func<int> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            return body();
        }
        catch (OperationCanceledException)
        {
            global::System.Console.Error.WriteLine(Messages.N_INT().Text);
            return (int)ExitCode.Interrupted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            global::System.Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return (int)ExitCode.Failure;
        }
    }

    internal static int Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = ArgParser.Parse(args);

        if (Dispatch(parsed) is { } early)
        {
            return early;
        }

        return RunConvert(parsed.Options!, ct);
    }

    private static int? Dispatch(ParsedCommand parsed) => parsed.Kind switch
    {
        CommandKind.Help => Print(HelpText.Render()),
        CommandKind.Version => Print(Messages.N_VERSION(VersionInfo.Current) + "\n"),
        CommandKind.Error => Fail(parsed.Error!),
        CommandKind.Setup or CommandKind.Doctor => Fail(Messages.E_TEMP_01()),
        _ when parsed.Options is { } o && (o.InstallDeps || o.Interactive || o.NoInstall) => Fail(Messages.E_TEMP_01()),
        _ => null,
    };

    private static int Print(string text)
    {
        global::System.Console.Out.Write(text);
        return (int)ExitCode.Ok;
    }

    private static int Fail(Message message)
    {
        global::System.Console.Error.WriteLine(message.Text);
        if (message.Hint is { } hint)
        {
            global::System.Console.Error.WriteLine($"  {hint}");
        }
        return message.ExitCode;
    }

    private static int RunConvert(RawOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var validation = OptionValidator.Validate(options, OperatingSystemKind.Linux);
        if (validation.Error is { } verr)
        {
            global::System.Console.Error.WriteLine(validation.Error.Text);
            if (validation.Error.Hint is { } vh) global::System.Console.Error.WriteLine($"  {vh}");
            return verr.ExitCode;
        }

        var os = PlatformDetector.DetectOs();
        var candidate = FfmpegLocator.Locate(
            options.FfmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(os, Environment.GetEnvironmentVariable),
            os);

        if (candidate is null)
        {
            // Package managers are looked up only now, when FFmpeg is missing.
            var platform = PlatformDetector.Detect();
            var suggestions = InstallAdvisor.Suggestions(platform);
            global::System.Console.Error.WriteLine(InstallAdvisor.NotFoundText(platform, suggestions));
            return 3;
        }

        var infoService = new FfmpegInfoService(new ProcessLauncher());
        var infoResult = infoService.GetInfoAsync(candidate.Path, ct).GetAwaiter().GetResult();
        if (infoResult.NotRunnable is { } notRunnable)
        {
            return Fail(Messages.E_FF_02(candidate.Path, notRunnable.Reason));
        }
        if (infoResult.TooOld is { } tooOld)
        {
            return Fail(Messages.E_FF_07(candidate.Path, tooOld.Version.ToString()));
        }

        var info = infoResult.Info!;

        var probe = new EncoderProbe(new ProcessLauncher(), infoService, os);
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
        var plan = ConversionPlanner.Build(planSources, outputFolder, fs, os);

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

        var ffprobe = FfprobeLocator.Find(candidate.Path, PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists, os);
        var runner = new ConversionRunner(candidate.Path, new ProcessLauncher(), new MediaProbe(new ProcessLauncher(), candidate.Path, ffprobe));
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
