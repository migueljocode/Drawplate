using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Console.Cli;
using Drawplate.Console.Ui;
using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
using Drawplate.Models.Formatting;
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

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var parsed = ArgParser.Parse(args);
        if (Dispatch(parsed, stdout, stderr) is { } early)
        {
            return early;
        }

        return RunConvert(parsed.Options!, stdout, stderr, ct);
    }

    private static int? Dispatch(ParsedCommand parsed, TextWriter stdout, TextWriter stderr) => parsed.Kind switch
    {
        CommandKind.Help => Print(stdout, HelpText.Render()),
        CommandKind.Version => Print(stdout, Messages.N_VERSION(VersionInfo.Current) + "\n"),
        CommandKind.Error => Fail(parsed.Error!, stderr),
        CommandKind.Setup or CommandKind.Doctor => Fail(Messages.E_TEMP_01(), stderr),
        _ when parsed.Options is { } o && (o.InstallDeps || o.Interactive || o.NoInstall) => Fail(Messages.E_TEMP_01(), stderr),
        _ => null,
    };

    private static int Print(TextWriter stdout, string text)
    {
        stdout.Write(text);
        return (int)ExitCode.Ok;
    }

    private static int Fail(Message message, TextWriter stderr)
    {
        stderr.WriteLine(message.Text);
        if (message.Hint is { } hint)
        {
            stderr.WriteLine($"  {hint}");
        }
        return message.ExitCode;
    }

    private static int RunConvert(RawOptions options, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var os = PlatformDetector.DetectOs();
        var validation = OptionValidator.Validate(options, os);
        if (validation.Options is not { } convert)
        {
            return Fail(validation.Error!, stderr);
        }

        var renderer = new PlainRenderer(stdout, Utf8Output());

        // Input and output problems are reported before FFmpeg is touched, so they start no process.
        WorkPlan? work = null;
        if (!options.ListEncoders)
        {
            var (plan, exit) = PlanWork(convert, os, renderer, stderr);
            if (exit is { } planExit)
            {
                return planExit;
            }
            work = plan;
        }

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
            stderr.WriteLine(InstallAdvisor.NotFoundText(platform, suggestions));
            return 3;
        }

        var infoService = new FfmpegInfoService(new ProcessLauncher());
        var infoResult = infoService.GetInfoAsync(candidate.Path, ct).GetAwaiter().GetResult();
        if (infoResult.NotRunnable is { } notRunnable)
        {
            return Fail(Messages.E_FF_02(candidate.Path, notRunnable.Reason), stderr);
        }
        if (infoResult.TooOld is { } tooOld)
        {
            return Fail(Messages.E_FF_07(candidate.Path, tooOld.Version.ToString()), stderr);
        }

        var info = infoResult.Info!;
        var log = options.Verbose ? (Action<string>)stderr.WriteLine : null;
        var probe = new EncoderProbe(new ProcessLauncher(), infoService, os, log: log);
        var results = probe.ProbeAsync(info, ProbeKinds(convert, os), ct).GetAwaiter().GetResult();

        var working = results.Where(r => r.Passed).Select(r => r.Kind).ToImmutableArray();
        var built = EncoderChain.Build(convert.Encoder is { } pinned ? EncoderSets.Id(pinned) : null, os, working);
        if (built.Error is { } chainError)
        {
            return Fail(chainError, stderr);
        }

        var chain = built.Chain;
        if (chain.IsEmpty)
        {
            stderr.WriteLine(Messages.E_FF_03().Text);
            return 3;
        }

        var note = HardwareNoteFor(results, chain);
        if (options.ListEncoders)
        {
            foreach (var kind in chain)
            {
                stdout.WriteLine(EncoderSets.Id(kind));
            }
            if (note is not null)
            {
                stderr.WriteLine(note);
            }
            return (int)ExitCode.Ok;
        }

        var vaapi = results.FirstOrDefault(r => r.Kind == EncoderKind.Vaapi && r.Passed)?.Vaapi;
        return Convert(work!, convert, candidate.Path, chain, vaapi, note, os, renderer, log, ct);
    }

    /// <summary>C.5: Unicode glyphs only when the console output is UTF-8 and TERM is not dumb.</summary>
    private static bool Utf8Output() =>
        global::System.Console.OutputEncoding.CodePage == 65001
        && !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.Ordinal);

    /// <summary>What to probe: all encoders for this OS, only the CPU for -c, or the pinned one plus the CPU.</summary>
    private static ImmutableArray<EncoderKind> ProbeKinds(ConvertOptions convert, OperatingSystemKind os)
    {
        if (convert.Encoder is null)
        {
            return EncoderSets.For(os);
        }

        return convert.Encoder == EncoderKind.Cpu
            ? [EncoderKind.Cpu]
            : [convert.Encoder.Value, EncoderKind.Cpu];
    }

    /// <summary>The D.7 note. Shown only when a hardware probe actually ran (a process was started and reported a result).</summary>
    private static string? HardwareNoteFor(ImmutableArray<ProbeResult> results, ImmutableArray<EncoderKind> chain)
    {
        var hardwareRan = results.Any(r => r.Kind != EncoderKind.Cpu
            && r.FailureKind is not (ProbeFailureKind.NotListed or ProbeFailureKind.NoDevice));
        if (!hardwareRan)
        {
            return null;
        }

        var anyHardwarePassed = chain.Any(kind => kind != EncoderKind.Cpu);
        var nvenc = results.FirstOrDefault(r => r.Kind == EncoderKind.Nvenc && r.FailureKind == ProbeFailureKind.NvencDriverTooOld);
        var minimumDriver = nvenc is null ? null : FailureClassifier.ParseNvenc(nvenc.StderrTail).MinimumDriver;

        return HardwareNote.For(GpuDetector.HasGpu(), anyHardwarePassed, nvenc is not null, minimumDriver);
    }

    private sealed record WorkPlan(string InputLabel, string OutputFolder, ConversionPlan Plan);

    /// <summary>Discovers inputs, plans them, and reports skips. Returns an exit code when the run must stop.</summary>
    private static (WorkPlan? Plan, int? Exit) PlanWork(ConvertOptions convert, OperatingSystemKind os, IRenderer renderer, TextWriter stderr)
    {
        var folderMode = convert.Files.IsEmpty;
        var inputFolder = convert.InputFolder ?? ".";

        ImmutableArray<string> sources;
        if (folderMode)
        {
            var (files, error) = InputDiscovery.FromFolder(inputFolder);
            if (error is not null)
            {
                return (null, Fail(error, stderr));
            }
            sources = files;
        }
        else
        {
            sources = convert.Files;
        }

        var outputFolder = Path.GetFullPath(
            convert.OutputFolder ?? (folderMode ? Path.Combine(inputFolder, "compressed") : Path.Combine(".", "compressed")));
        if (File.Exists(outputFolder))
        {
            return (null, Fail(Messages.E_USAGE_08(outputFolder), stderr));
        }

        var plan = ConversionPlanner.Build(sources, outputFolder, new RealPlanFileSystem(), os);

        foreach (var file in plan.Files)
        {
            switch (file.Status)
            {
                case PlanStatus.Missing:
                    stderr.WriteLine(Messages.W_01(Sanitizer.ForDisplay(file.Source)).Text);
                    break;
                case PlanStatus.NotAFile:
                    stderr.WriteLine(Messages.W_02(Sanitizer.ForDisplay(file.Source)).Text);
                    break;
                case PlanStatus.SkipUnsupported:
                    renderer.Unsupported(Path.GetFileName(file.Source));
                    break;
            }
        }

        if (plan.Collisions.Length > 0)
        {
            return (null, Fail(Messages.E_USAGE_09(plan.Collisions), stderr));
        }

        var anything = plan.Files.Any(f => f.Status is PlanStatus.Convert or PlanStatus.SkipExists or PlanStatus.SkipInsideOutput);
        if (!anything)
        {
            return (null, Fail(Messages.E_RUN_01(), stderr));
        }

        var inputLabel = folderMode ? inputFolder : $"{sources.Length} file(s)";
        return (new WorkPlan(inputLabel, outputFolder, plan), null);
    }

    private static int Convert(
        WorkPlan work,
        ConvertOptions convert,
        string ffmpegPath,
        ImmutableArray<EncoderKind> chain,
        VaapiTarget? vaapi,
        string? note,
        OperatingSystemKind os,
        IRenderer renderer,
        Action<string>? log,
        CancellationToken ct)
    {
        // Files that are listed and numbered: the ones to convert and the ones skipped for a known reason.
        // Missing files, directories and unsupported types were already reported as W-01, W-02 and N-01.
        var listed = work.Plan.Files
            .Where(f => f.Status is PlanStatus.Convert or PlanStatus.SkipExists or PlanStatus.SkipInsideOutput)
            .ToImmutableArray();
        Directory.CreateDirectory(work.OutputFolder);

        var totalInputSize = listed.Sum(f => SizeOf(f.Source));
        renderer.PlanCard(
            work.InputLabel,
            work.OutputFolder,
            convert.Quality,
            QualityScale.ToCrf(convert.Quality),
            SizeText(convert.MaxSize),
            string.Join(" > ", chain.Select(EncoderSets.Badge)),
            listed.Length,
            totalInputSize,
            !convert.NoTimestamps);

        if (note is not null)
        {
            renderer.Note(note);
        }

        var ffprobe = FfprobeLocator.Find(ffmpegPath, PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists, os);
        var runner = new ConversionRunner(ffmpegPath, new ProcessLauncher(), new MediaProbe(new ProcessLauncher(), ffmpegPath, ffprobe));

        var converted = 0;
        var failed = 0;
        var skipped = 0;
        long inputBytes = 0;
        long outputBytes = 0;
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var clock = Stopwatch.StartNew();

        for (var index = 0; index < listed.Length; index++)
        {
            var file = listed[index];
            var number = index + 1;
            var name = Path.GetFileName(file.Source);

            if (file.Status != PlanStatus.Convert)
            {
                skipped++;
                renderer.FileSkipped(number, listed.Length, name, SkipReason(file.Status));
                continue;
            }

            var result = runner.RunAsync(file.Source, work.OutputFolder, chain, convert.Quality, convert.MaxSize, convert.NoTimestamps, vaapi, log, null, ct)
                .GetAwaiter().GetResult();

            switch (result.Status)
            {
                case FileResultStatus.Converted:
                    converted++;
                    inputBytes += result.InputSize;
                    outputBytes += result.OutputSize;
                    used[result.Encoder!] = used.GetValueOrDefault(result.Encoder!) + 1;
                    renderer.FileFinished(number, listed.Length, name, true, BadgeFor(result.Encoder!, chain), result.Elapsed, result.InputSize, result.OutputSize, null);
                    break;
                case FileResultStatus.Skipped:
                    skipped++;
                    renderer.FileSkipped(number, listed.Length, name, result.Reason ?? "output already exists");
                    break;
                default:
                    failed++;
                    renderer.FileFinished(number, listed.Length, name, false, null, result.Elapsed, result.InputSize, 0, result.Reason);
                    break;
            }
        }

        var encoderCounts = string.Join(" ", chain
            .Where(kind => used.ContainsKey(EncoderSets.Id(kind)))
            .Select(kind => $"{EncoderSets.Badge(kind)} x{used[EncoderSets.Id(kind)].ToString(CultureInfo.InvariantCulture)}"));
        renderer.Summary(converted, failed, skipped, inputBytes, outputBytes, encoderCounts, clock.Elapsed, work.OutputFolder);
        return failed > 0 ? (int)ExitCode.Failure : (int)ExitCode.Ok;
    }

    private static string SkipReason(PlanStatus status) =>
        status == PlanStatus.SkipInsideOutput ? "already in the output folder" : "output already exists";

    private static string BadgeFor(string encoderId, ImmutableArray<EncoderKind> chain) =>
        chain.Where(kind => EncoderSets.Id(kind) == encoderId).Select(EncoderSets.Badge).FirstOrDefault() ?? "[CPU]";

    private static long SizeOf(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string? SizeText(SizeCap cap)
    {
        if (cap == SizeCap.None)
        {
            return null;
        }

        var (width, height) = SizeCaps.Box(cap);
        return $"{cap.ToString().ToLowerInvariant()} {width}x{height}";
    }
}
