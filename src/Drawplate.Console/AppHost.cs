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
using Drawplate.Models.Ffmpeg;
using Drawplate.Models.Formatting;
using Drawplate.Models.Planning;
using Drawplate.Models.Setup;
using Drawplate.Services.Conversion;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Interaction;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;
using Drawplate.Services.Setup;

namespace Drawplate.Console;

/// <summary>Top-level CLI composition root. Keeps all side effects behind interfaces and handles top-level errors cleanly.</summary>
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
            ScreenGuard.EmergencyRestore();
            global::System.Console.Error.WriteLine(Messages.N_INT().Text);
            return (int)ExitCode.Interrupted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ScreenGuard.EmergencyRestore();
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
        if (Dispatch(parsed, stdout, stderr, ct) is { } early)
        {
            return early;
        }

        return RunConvert(parsed.Options!, stdout, stderr, ct);
    }

    private static int Print(TextWriter writer, string text)
    {
        writer.Write(text);
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

    private static int? Dispatch(ParsedCommand parsed, TextWriter stdout, TextWriter stderr, CancellationToken ct) => parsed.Kind switch
    {
        CommandKind.Help => RunHelp(stdout),
        CommandKind.Version => Print(stdout, Messages.N_VERSION(VersionInfo.Current) + "\n"),
        CommandKind.Error => Fail(parsed.Error!, stderr),
        CommandKind.Setup => RunSetup(parsed.Options!, stdout, stderr, ct),
        CommandKind.Doctor => RunDoctor(parsed.Options!, stdout, stderr, ct),
        _ => null,
    };

    private static int RunHelp(TextWriter stdout)
    {
        var theme = CreateTheme(stdout);
        if (theme.Colors)
        {
            HelpView.Show(stdout, theme);
            return (int)ExitCode.Ok;
        }

        return Print(stdout, HelpText.Render());
    }

    private static int RunSetup(RawOptions options, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        var platform = PlatformDetector.Detect(PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists);
        using var consoleGuard = ReferenceEquals(stdout, global::System.Console.Out) && !global::System.Console.IsOutputRedirected
            ? ConsoleGuard.EnterInteractive()
            : null;
        var theme = CreateTheme(stdout, consoleGuard);
        if (theme.Colors)
        {
            RichStatusView.WriteSetupBanner(stdout, theme, platform);
        }

        var info = new FfmpegInfoService(new ProcessLauncher());
        var flow = new AcquisitionFlow(info, CreatePrompt(stdout), new InstallRunner(new ProcessLauncher()), platform, stdout, stderr);
        var result = flow.RunAsync(AcquisitionMode.SetupCommand, options.InstallDeps, options.NoInstall, options.FfmpegPath, ct)
            .GetAwaiter().GetResult();
        return result.ExitCode;
    }

    private static int RunDoctor(RawOptions options, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        var platform = PlatformDetector.Detect(PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists);
        using var consoleGuard = ReferenceEquals(stdout, global::System.Console.Out) && !global::System.Console.IsOutputRedirected
            ? ConsoleGuard.EnterInteractive()
            : null;
        var theme = CreateTheme(stdout, consoleGuard);

        if (theme.Colors)
        {
            RichStatusView.WriteDoctorBanner(stdout, theme);
        }

        var info = new FfmpegInfoService(new ProcessLauncher());
        var doctorService = new DoctorService(info, new ProcessLauncher(), platform);
        var report = theme.Colors
            ? RichStatusView.RunWithSpinner(stdout, theme, "Checking FFmpeg, ffprobe & GPU encoders...", token => doctorService.RunAsync(options.FfmpegPath, token), ct)
            : doctorService.RunAsync(options.FfmpegPath, ct).GetAwaiter().GetResult();

        if (theme.Colors)
        {
            RichStatusView.WriteDoctor(stdout, report, platform, theme, includeBanner: false);
            return report.ExitCode;
        }

        foreach (var line in report.Lines)
        {
            stdout.WriteLine($"{line.Status,-8}  {line.Check,-10}  {line.Detail}");
        }

        if (report.Error is { } error)
        {
            return Fail(error, stderr);
        }

        if (report.Lines.Any(line => line.Check == "FFmpeg" && line.Status == "missing"))
        {
            stderr.WriteLine(InstallAdvisor.NotFoundText(platform, InstallAdvisor.Suggestions(platform)));
        }

        return report.ExitCode;
    }

    private static ConsolePrompt CreatePrompt(TextWriter output) =>
        new ConsolePrompt(
            global::System.Console.In,
            output,
            !global::System.Console.IsInputRedirected,
            !global::System.Console.IsOutputRedirected);

    private static int RunConvert(RawOptions options, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var os = PlatformDetector.DetectOs();
        var validation = OptionValidator.Validate(options, os);
        if (validation.Options is not { } convert)
        {
            return Fail(validation.Error!, stderr);
        }

        Action<string>? log = options.Verbose ? stderr.WriteLine : null;
        if (options.InstallDeps)
        {
            return RunSetup(options, stdout, stderr, ct);
        }

        if (options.Interactive && !CreatePrompt(stdout).IsInteractive)
        {
            return Fail(Messages.E_USAGE_10(), stderr);
        }

        using var consoleGuard = ReferenceEquals(stdout, global::System.Console.Out) && !global::System.Console.IsOutputRedirected
            ? ConsoleGuard.EnterInteractive()
            : null;
        var theme = CreateTheme(stdout, consoleGuard);
        var plainRenderer = new PlainRenderer(stdout, theme.Utf8Glyphs);

        // Input and output problems are reported before FFmpeg is touched, so they start no process.
        WorkPlan? work = null;
        if (!options.ListEncoders && !options.Interactive)
        {
            var (plan, exit) = PlanWork(convert, os, plainRenderer, stderr);
            if (exit is { } planExit)
            {
                return planExit;
            }

            work = plan;
        }

        var platformInfo = PlatformDetector.Detect(PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")), File.Exists, os);
        var infoService = new FfmpegInfoService(new ProcessLauncher());
        var acquisition = new AcquisitionFlow(
            infoService,
            CreatePrompt(stdout),
            new InstallRunner(new ProcessLauncher()),
            platformInfo,
            stdout,
            stderr);

        var useFullScreen = !options.ListEncoders && !options.Verbose && theme.Colors && theme.CanRedraw && theme.IsInteractive;
        var probe = new EncoderProbe(new ProcessLauncher(), infoService, os, log: log);
        var probeKinds = options.Interactive ? EncoderSets.For(os) : ProbeKinds(convert, os);

        if (useFullScreen)
        {
            return RunConvertFullScreen(
                stdout,
                stderr,
                theme,
                options,
                convert,
                work,
                os,
                acquisition,
                infoService,
                probe,
                probeKinds,
                log,
                ct);
        }

        var acquired = options.ListEncoders && theme.Colors
            ? RichStatusView.RunWithSpinner(stdout, theme, "Probing FFmpeg...", token => acquisition.RunAsync(AcquisitionMode.Convert, options.InstallDeps, options.NoInstall, options.FfmpegPath, token), ct)
            : acquisition.RunAsync(AcquisitionMode.Convert, options.InstallDeps, options.NoInstall, options.FfmpegPath, ct).GetAwaiter().GetResult();
        if (acquired.Status == AcquisitionStatus.Exit)
        {
            return acquired.ExitCode;
        }

        var candidate = acquired.Candidate!;
        var infoResult = acquired.InfoResult ?? infoService.GetInfoAsync(candidate.Path, ct).GetAwaiter().GetResult();
        if (infoResult.NotRunnable is { } notRunnable)
        {
            return Fail(Messages.E_FF_02(candidate.Path, notRunnable.Reason), stderr);
        }

        if (infoResult.TooOld is { } tooOld)
        {
            return Fail(Messages.E_FF_07(tooOld.Version.ToString(), os), stderr);
        }

        var info = infoResult.Info!;
        var results = options.ListEncoders && theme.Colors
            ? RichStatusView.RunWithSpinner(stdout, theme, "Probing available encoders...", token => probe.ProbeAsync(info, probeKinds, token), ct)
            : probe.ProbeAsync(info, probeKinds, ct).GetAwaiter().GetResult();

        var working = results.Where(r => r.Passed).Select(r => r.Kind).ToImmutableArray();
        var built = EncoderChain.Build(
            !options.Interactive && convert.Encoder is { } pinned ? EncoderSets.Id(pinned) : null,
            os,
            working);
        if (built.Error is { } chainError)
        {
            return Fail(chainError, stderr);
        }

        var chain = built.Chain;
        if (options.Interactive)
        {
            var menu = new MenuService(CreatePrompt(stdout)).Run(working, convert.Quality, convert.MaxSize);
            if (menu.Error is { } menuError)
            {
                if (menuError.Id == "N-INT")
                {
                    stderr.WriteLine(menuError.Text);
                    return menuError.ExitCode;
                }

                return Fail(menuError, stderr);
            }

            var selected = menu.Encoder;
            if (selected is { } selectedEncoder)
            {
                var selectedBuild = EncoderChain.Build(EncoderSets.Id(selectedEncoder), os, working);
                if (selectedBuild.Error is { } selectedError)
                {
                    return Fail(selectedError, stderr);
                }

                chain = selectedBuild.Chain;
            }

            convert = new ConvertOptions
            {
                Encoder = selected,
                Quality = menu.Quality ?? convert.Quality,
                MaxSize = menu.MaxSize ?? convert.MaxSize,
                InputFolder = convert.InputFolder,
                Files = convert.Files,
                NoTimestamps = convert.NoTimestamps,
                OutputFolder = convert.OutputFolder,
            };
        }

        var renderer = RendererFactory.Create(stdout, theme);
        if (!options.ListEncoders && work is null)
        {
            var (plan, exit) = PlanWork(convert, os, renderer, stderr);
            if (exit is { } planExit)
            {
                return planExit;
            }

            work = plan;
        }

        if (chain.IsEmpty)
        {
            return Fail(Messages.E_FF_03(os), stderr);
        }

        var note = HardwareNoteFor(results, chain);
        if (options.ListEncoders)
        {
            if (theme.Colors)
            {
                RichStatusView.WriteEncoders(stdout, chain, note, theme);
                return (int)ExitCode.Ok;
            }

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

    private static int RunConvertFullScreen(
        TextWriter stdout,
        TextWriter stderr,
        Theme theme,
        RawOptions options,
        ConvertOptions convert,
        WorkPlan? work,
        OperatingSystemKind os,
        AcquisitionFlow acquisition,
        FfmpegInfoService infoService,
        EncoderProbe probe,
        ImmutableArray<EncoderKind> probeKinds,
        Action<string>? log,
        CancellationToken ct)
    {
        // Check FFmpeg availability first; if missing and promptable, let AcquisitionFlow prompt on normal console cleanly.
        var preLocated = FfmpegLocator.Locate(
            options.FfmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(os, Environment.GetEnvironmentVariable),
            os);

        AcquisitionOutcome? preAcquired = null;
        if (preLocated is null)
        {
            preAcquired = acquisition.RunAsync(AcquisitionMode.Convert, options.InstallDeps, options.NoInstall, options.FfmpegPath, ct).GetAwaiter().GetResult();
            if (preAcquired.Status == AcquisitionStatus.Exit)
            {
                return preAcquired.ExitCode;
            }
        }

        var clock = Stopwatch.StartNew();
        FfmpegCandidate? candidate = null;
        FfmpegInfo? info = null;
        ImmutableArray<ProbeResult> results = default;
        ImmutableArray<EncoderKind> chain = default;
        VaapiTarget? vaapi = null;
        string? note = null;
        RichDashboard.SummaryInfo? summary = null;
        int exitCode;

        using (var screen = ScreenGuard.TryEnter(stdout, theme, enableMouse: true))
        {
            var probeOutcome = RichDashboard.RunStartupTask(
                stdout,
                theme,
                clock,
                "Detecting FFmpeg & hardware encoders...",
                async token =>
                {
                    var acq = preAcquired ?? await acquisition.RunAsync(AcquisitionMode.Convert, options.InstallDeps, options.NoInstall, options.FfmpegPath, token).ConfigureAwait(false);
                    if (acq.Status == AcquisitionStatus.Exit)
                    {
                        return (Exit: acq.ExitCode, Error: (Message?)null, Candidate: (FfmpegCandidate?)null, Info: (FfmpegInfo?)null, Results: ImmutableArray<ProbeResult>.Empty);
                    }

                    var cand = acq.Candidate!;
                    var infoRes = acq.InfoResult ?? await infoService.GetInfoAsync(cand.Path, token).ConfigureAwait(false);
                    if (infoRes.NotRunnable is { } nr)
                    {
                        return (Exit: 0, Error: Messages.E_FF_02(cand.Path, nr.Reason), Candidate: cand, Info: (FfmpegInfo?)null, Results: ImmutableArray<ProbeResult>.Empty);
                    }

                    if (infoRes.TooOld is { } old)
                    {
                        return (Exit: 0, Error: Messages.E_FF_07(old.Version.ToString(), os), Candidate: cand, Info: (FfmpegInfo?)null, Results: ImmutableArray<ProbeResult>.Empty);
                    }

                    var probed = await probe.ProbeAsync(infoRes.Info!, probeKinds, token).ConfigureAwait(false);
                    return (Exit: 0, Error: (Message?)null, Candidate: cand, Info: infoRes.Info, Results: probed);
                },
                ct);

            if (probeOutcome.Info is null)
            {
                screen?.Dispose();
                return probeOutcome.Error is { } err ? Fail(err, stderr) : probeOutcome.Exit;
            }

            candidate = probeOutcome.Candidate!;
            info = probeOutcome.Info;
            results = probeOutcome.Results;

            var working = results.Where(r => r.Passed).Select(r => r.Kind).ToImmutableArray();
            var built = EncoderChain.Build(
                !options.Interactive && convert.Encoder is { } pinned ? EncoderSets.Id(pinned) : null,
                os,
                working);
            if (built.Error is { } chainError)
            {
                screen?.Dispose();
                return Fail(chainError, stderr);
            }

            chain = built.Chain;
            if (options.Interactive)
            {
                var menu = RichInteractiveMenu.Run(stdout, theme, working, convert.Quality, convert.MaxSize, clock);
                if (menu.Error is { } menuError)
                {
                    screen?.Dispose();
                    if (menuError.Id == "N-INT")
                    {
                        stderr.WriteLine(menuError.Text);
                        return menuError.ExitCode;
                    }

                    return Fail(menuError, stderr);
                }

                var selected = menu.Encoder;
                if (selected is { } selectedEncoder)
                {
                    var selectedBuild = EncoderChain.Build(EncoderSets.Id(selectedEncoder), os, working);
                    if (selectedBuild.Error is { } selectedError)
                    {
                        screen?.Dispose();
                        return Fail(selectedError, stderr);
                    }

                    chain = selectedBuild.Chain;
                }

                convert = new ConvertOptions
                {
                    Encoder = selected,
                    Quality = menu.Quality ?? convert.Quality,
                    MaxSize = menu.MaxSize ?? convert.MaxSize,
                    InputFolder = convert.InputFolder,
                    Files = convert.Files,
                    NoTimestamps = convert.NoTimestamps,
                    OutputFolder = convert.OutputFolder,
                };
            }

            if (work is null)
            {
                var planRenderer = new RichDashboard.DashboardRenderer(theme.Utf8Glyphs);
                var (plan, exit) = PlanWork(convert, os, planRenderer, stderr);
                if (exit is { } planExit)
                {
                    screen?.Dispose();
                    return planExit;
                }

                work = plan;
            }

            if (chain.IsEmpty)
            {
                screen?.Dispose();
                return Fail(Messages.E_FF_03(os), stderr);
            }

            note = HardwareNoteFor(results, chain);
            vaapi = results.FirstOrDefault(r => r.Kind == EncoderKind.Vaapi && r.Passed)?.Vaapi;
            (exitCode, summary) = RichDashboard.RunConversionLoop(
                stdout,
                theme,
                clock,
                (dashRenderer, token) => Convert(work!, convert, candidate!.Path, chain, vaapi, note, os, dashRenderer, log, token),
                ct);
        }

        // Print final Aurora summary card onto normal stdout so results & output folder persist in shell history.
        if (summary is { } s)
        {
            var rich = new RichRenderer(stdout, theme);
            if (note is not null)
            {
                rich.Note(note);
            }

            rich.Summary(s.Converted, s.Failed, s.Skipped, s.InputBytes, s.OutputBytes, s.EncoderCounts, s.Elapsed, s.OutputFolder);
        }

        return exitCode;
    }

    private static Theme CreateTheme(TextWriter stdout, ConsoleGuard? consoleGuard = null)
    {
        var isConsoleOut = ReferenceEquals(stdout, global::System.Console.Out) && !global::System.Console.IsOutputRedirected;
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NO_COLOR"] = Environment.GetEnvironmentVariable("NO_COLOR"),
            ["TERM"] = Environment.GetEnvironmentVariable("TERM"),
        };
        var detected = Theme.Detect(
            isConsoleOut,
            environment,
            global::System.Console.OutputEncoding.CodePage,
            ConsoleWidth(),
            !global::System.Console.IsInputRedirected);
        if (consoleGuard is not null && !consoleGuard.VtEnabled)
        {
            return detected with { Colors = false, CanRedraw = false };
        }

        return detected;
    }

    private static int? ConsoleWidth()
    {
        try
        {
            return global::System.Console.WindowWidth;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static OperatingSystemKind CurrentOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return OperatingSystemKind.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return OperatingSystemKind.MacOS;
        }

        return OperatingSystemKind.Linux;
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
            ct.ThrowIfCancellationRequested();

            var file = listed[index];
            var number = index + 1;
            var name = Path.GetFileName(file.Source);

            if (file.Status != PlanStatus.Convert)
            {
                skipped++;
                renderer.FileSkipped(number, listed.Length, name, SkipReason(file.Status));
                continue;
            }

            renderer.FileStarted(number, listed.Length, $"{name}  {EncoderSets.Badge(chain[0])}");
            FileProgressHandler progress = (string _, double? fraction, double? speed, TimeSpan elapsed) =>
                renderer.FileProgress(number, listed.Length, name, fraction, speed, elapsed);
            var result = runner.RunAsync(file.Source, work.OutputFolder, chain, convert.Quality, convert.MaxSize, convert.NoTimestamps, vaapi, log, progress, ct)
                .GetAwaiter().GetResult();

            // A signal kills ffmpeg, which the runner sees as an ordinary failed file. Stop here instead of reporting it (F23).
            ct.ThrowIfCancellationRequested();

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
