using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using Drawplate.Console.Cli;
using Drawplate.Console.Ui;
using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
using Drawplate.Models.Failures;
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

        // Input and output problems are reported before FFmpeg is touched, so they start no process.
        WorkPlan? work = null;
        if (!options.ListEncoders)
        {
            var (plan, exit) = PlanWork(convert, os, stdout, stderr);
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

        return Convert(work!, convert, candidate.Path, chain, note, os, stdout, ct);
    }

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
    private static (WorkPlan? Plan, int? Exit) PlanWork(ConvertOptions convert, OperatingSystemKind os, TextWriter stdout, TextWriter stderr)
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
                    stderr.WriteLine(Messages.W_01(file.Source).Text);
                    break;
                case PlanStatus.NotAFile:
                    stderr.WriteLine(Messages.W_02(file.Source).Text);
                    break;
                case PlanStatus.SkipUnsupported:
                    stdout.WriteLine($"- {Path.GetFileName(file.Source)} (unsupported file type)");
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

        var inputLabel = folderMode ? inputFolder : "files";
        return (new WorkPlan(inputLabel, outputFolder, plan), null);
    }

    private static int Convert(
        WorkPlan work,
        ConvertOptions convert,
        string ffmpegPath,
        ImmutableArray<EncoderKind> chain,
        string? note,
        OperatingSystemKind os,
        TextWriter stdout,
        CancellationToken ct)
    {
        var toConvert = work.Plan.Files.Where(f => f.Status == PlanStatus.Convert).ToImmutableArray();
        var skipCount = work.Plan.Files.Length - toConvert.Length;
        Directory.CreateDirectory(work.OutputFolder);

        var totalInputSize = toConvert.Sum(f => new FileInfo(f.Source).Length);
        var chainText = string.Join(" > ", chain.Select(EncoderSets.Badge));
        var renderer = new PlainRenderer(global::System.Console.OutputEncoding.CodePage == 65001);
        renderer.PlanCard(
            work.InputLabel,
            work.OutputFolder,
            convert.Quality,
            QualityScale.ToCrf(convert.Quality),
            SizeText(convert.MaxSize),
            chainText,
            toConvert.Length,
            totalInputSize,
            !convert.NoTimestamps);

        if (note is not null)
        {
            stdout.WriteLine(note);
        }

        var ffprobe = FfprobeLocator.Find(ffmpegPath, PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists, os);
        var runner = new ConversionRunner(ffmpegPath, new ProcessLauncher(), new MediaProbe(new ProcessLauncher(), ffmpegPath, ffprobe));

        var converted = 0;
        var failed = 0;
        for (var index = 0; index < toConvert.Length; index++)
        {
            var file = toConvert[index];
            var result = runner.RunAsync(file.Source, work.OutputFolder, chain, convert.Quality, convert.MaxSize, convert.NoTimestamps, null, ct)
                .GetAwaiter().GetResult();

            if (result.Status == FileResultStatus.Converted)
            {
                converted++;
            }
            else if (result.Status == FileResultStatus.Failed)
            {
                failed++;
            }

            var glyph = result.Status == FileResultStatus.Converted ? "✓" : "✗";
            stdout.WriteLine($"{glyph} [{index + 1}/{toConvert.Length}] {Path.GetFileName(file.Source)}");
        }

        renderer.Summary(converted, failed, skipCount, totalInputSize, 0, chainText, TimeSpan.Zero, work.OutputFolder);
        return failed > 0 ? (int)ExitCode.Failure : (int)ExitCode.Ok;
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
