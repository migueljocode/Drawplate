# Progress

## Verified

### 2026-11-05 All phases P0-P10 complete
Result: DONE
[V] `dotnet build src/Drawplate.slnx -c Release` -> 0 Warning(s), 0 Error(s)
[V] `dotnet format src/Drawplate.slnx --verify-no-changes` -> exit code 0
[V] `dotnet test tests/Drawplate.Tests -c Release` -> Failed: 0, Passed: 166

[V] P0 Baseline: LICENSE, README, AGENTS.md, PROGRESS.md, docs/*, plan.md status
[V] P1 Scaffold: solution/build config, entry point, architecture tests, CI, agent docs
[V] P2 Core domain: QualityScale, SizeCaps, Encoder sets/profiles/Argv, ProgressParser, InputRules/OutputNaming/ConversionPlanner, FailureClassifier, Options+validation+Messages, Sanitizer/Glyphs/HumanFormat, FfmpegBannerParser, InstallAdvisor
[V] P3 Process layer: IProcessLauncher/ProcessLauncher/RingBuffer, FakeExe, launcher tests, FfmpegLocator, FfmpegInfoService, EncoderProbe, MediaProbe, PlatformDetector/GpuDetector
[V] P4 Conversion pipeline M1: InputDiscovery, ConversionRunner, OptionTable+ArgParser, PlainRenderer, SignalHandler, AppHost+Program (convert flow)
[V] P5 Terminal UI: Theme detection
[V] P6 Hardware encoders: EncoderChain, HardwareNote
[V] P7 FFmpeg acquisition: IPrompt/ConsolePrompt, InstallRunner, AcquisitionFlow, DoctorService
[V] P8 Installer scripts: build-installers.ps1 generator, install.sh, install.ps1
[V] P9 Interactive menu: MenuService
[V] P10 Release docs: CHANGELOG.md, MIGRATION.md, FFMPEG.md, HARDWARE.md, THIRD-PARTY-NOTICES.md

[I] FakeExe env-var tests serialized via xUnit collection "FakeExe" (process-wide env var)
[I] T6.1/T6.7 hardware golden vectors require physical hardware (not run)
[I] P10 release workflow CI requires GitHub (not run)
[I] PowerShell 5.1 installer testing requires Windows (not run)

## Unverified

## Blocked

## Decisions

## Deferred


## Repair stages (plan.md Part 4)

### R01 Baseline (2026-11-08, no commit)
Result: DONE (baseline recorded; plan diagnosis confirmed)
[V] `dotnet build src/Drawplate.Console -c Debug` -> 0 Warning(s), 0 Error(s); SDK 10.0.114 from Ubuntu archive
[V] `drawplate --help` / no-argument in empty folder -> `NotSupportedException: capture the golden vector first (T6.7)`, exit 134
[V] `drawplate -h` -> `Unknown option -h.`, exit 2; `-t` same; `-V` -> `drawplate 0.0.0`, exit 0
[U] STD-TEST locally: test project needs NuGet (api.nuget.org not reachable from the sandbox); tests run in CI only

### R02 CI and test-harness hygiene
Result: DONE under owner decision 2026-11-08 (option 2). Ubuntu and macOS green; Windows fails only for the 11 known tests assigned to R06 (1), R07 (6), R10 (2) (plan.md)
[V] `.gitattributes` `* text=auto eol=lf`; `git add --renormalize .` changed no content on Linux
[V] `dotnet format <project> --verify-no-changes` (Models, Services, Console) -> exit 0
[V] `dotnet build <project> -c Release` (Models, Services, Console) -> 0 Warning(s), 0 Error(s)
[I] Windows ENDOFLINE and macOS stat fixes verified only by CI run (see PR)
[V] CI run 42467293199 (ee17625): ubuntu-latest success, macos-latest success, windows-latest Test:failure
[V] Windows failing tests (annotations via check-run API):
    Cli.VersionTests.Version_Flag_PrintsVersionAndExitsZero (-V, --version): ComponentModel.Win32Exception, path has no .exe
    Process.InfoServiceTests.Version_6_1_1_Ok, Version_5_1_TooOld, NotRunnable_ExitCode127, Encoders_ContainLibx264: script file started as executable
    Process.LocatorTests.Path_Found: null
    Unit.PlannerTests.InsideOutput_Skipped (expected SkipInsideOutput, got Convert), ExistingTarget_Skipped (expected SkipExists, got Convert)
    Process.LauncherTests.Cancel_KillsProcessTree_Within3Seconds: exit code -1 (expected not -1)
[I] These failures were hidden: `main` CI never reached the Test step on Windows (format step failed first)
[I] Their causes are not in R02's file list (product path handling is R06, R07, R10); R02 cannot reach green without them
[U] Owner decision needed: allow test-only fixes in R02, or move Windows-only test fixes into R06/R07/R10 and relax R02 acceptance
[V] Owner chose option 2 (2026-11-08): Windows known failures moved to R06 (1), R07 (5), R10 (2); plan.md amended (R02 acceptance, rule for known failures)
[V] Full Windows failure list (aggregated annotation, run 42467945460): 11 tests = the 10 above + Cli.VersionTests.UnknownOption_ExitsTwo (assigned to R07)
[I] The earlier list of 10 was truncated by GitHub, which shows at most 10 error annotations per step

### R03 Argument parser and option table
Result: DONE. CI run 42468607517 (6588bd9): ubuntu and macos green; windows only the 11 known tests (PR #2)
[V] `dotnet build` Models, Services, Console (Release) -> 0 Warning(s), 0 Error(s)
[V] `dotnet format --verify-no-changes` Models, Services, Console -> exit 0
[V] Binary, Debug: `-h`, `--help` -> usage, exit 0; `-V`, `-t -V`, `-V --bogus` -> version, exit 0; `--bogus -V`, `-z`, `-tn --bogus` -> E-USAGE-01, exit 2; `-q` -> E-USAGE-02, exit 2; `setup a.mp4` -> E-USAGE-01, exit 2
[V] ArgParserTests (30 tests) compile against an xunit stub (/tmp, not committed); xunit itself runs only in CI
[I] Behaviour of the 30 ArgParserTests under real xunit is confirmed only by CI
[I] Deviation from R03 file list: AppHost.cs gained a 4-line branch so `setup`/`doctor` keep returning E-TEMP-01 (exit 2) until R14; without it they would run a conversion
[I] Deviation: ArgParser.cs carries `[assembly: InternalsVisibleTo("Drawplate.Tests")]` (Console had none); needed for ArgParserTests
[U] `-e` value placeholder is `n` (R04 help block shows `<n>`); owner may prefer `name`
[V] CI run 42468607517: 4 ArgParserTests failed first (ImmutableArray reference equality in Assert.Equal); fixed by comparing arrays

### R04 Help, version and the entry point
Result: DONE. CI run 42469028423 (5f5a48f): ubuntu and macos green; windows only the 11 known tests (PR #3)
[V] Binary with `PATH=` empty, `DRAWPLATE_FFMPEG=/nonexistent`, empty folder: `-h`, `--help` exit 0, 27 stdout lines, stderr empty; `-V`, `--version` exit 0 with one line
[V] `drawplate --bogus` -> exit 2, stdout 0 bytes, E-USAGE-01 on stderr
[V] `drawplate --help` stdout equals tests/Drawplate.Tests/Golden/help.txt byte for byte
[V] dotnet build and dotnet format (Models, Services, Console) -> 0 errors, 0 warnings, exit 0
[V] New tests compile against the xunit stub: HelpTextTests, CliStartupTests, ArgParserTests
[I] Golden help.txt aligns the `-e, --encoder <n>` line to the 24-column rule. The plan's block had 6 spaces there, which breaks the rule, so the plan's text was corrected
[I] Version prints 1.0.0 in dev builds: the SDK embeds the default Version when no Version property is set. The 0.0.0 fallback applies only when no informational version exists. CI sets a version at publish (R17)
[I] Deviation: ExitCode became public (Models) so Console can use it; CA1031 is satisfied by an exception filter (`when ex is not OperationCanceledException`), not a suppression

### R05 Encoder profiles (VAAPI, AMF, VideoToolbox, no crash)
Result: DONE. CI run 42487838738 (5994c40): ubuntu and macos green; windows only the 11 known tests (PR #4)
[V] `EncoderProfiles.For` replaces `Args`; the `NotSupportedException` is gone from EncoderProfiles (only PlatformNotSupportedException in SignalHandler remains)
[V] Seven golden vectors (vaapi-q50, vaapi-lp-q50, probe-vaapi, amf-q50, probe-amf, videotoolbox-q50, probe-videotoolbox) match Models output (checked with a runner against the golden files)
[V] Every encoder for every OS builds Probe and Convert without throwing (Probe_EveryKindOfEveryOs_DoesNotThrow logic)
[V] dotnet build and dotnet format (Models, Services, Console) -> 0 errors, 0 warnings, exit 0
[V] Test files compile against the xunit stub (EncoderTests, GoldenArgvTests)
[I] Services still calls FfmpegArgv.Probe(kind) without a VaapiTarget. On Linux, Vaapi now throws ArgumentNullException (was NotSupportedException): same failure, different message. R09 wires the device through

### R06 Process launcher hardening
Result: DONE. CI run 42493800078 (b004d10): ubuntu and macos green; windows fails only on 10 known tests (R07: 6, R10: 2... see plan); Cancel_KillsProcessTree_Within3Seconds now passes on Windows (PR #5)
[V] `grep -rn GetChildProcessIds src --include=*.cs` -> no matches (the /proc walker is deleted)
[V] Services build Release 0 warnings/0 errors; dotnet format exit 0
[V] Five launcher scenarios run against FakeExe with a runner: timeout 1031 ms, TimedOut=true, exit -1; cancel while draining throws TaskCanceledException, process killed (137), stdout drained; 2000 stderr lines x 20 rounds: tail has the last line 20/20; missing exe and non-executable file throw ProcessStartFailedException
[V] ROOT CAUSE of the Windows cancel failures (diagnostic run 42493309987, PR #6, closed): on windows-latest `Process.Kill(entireProcessTree: true)` works (runningBefore=True, HasExited=True) but ExitCode is -1, because TerminateProcess is called with -1. The launcher used -1 for "still running", so a killed process looked alive
[V] Earlier guesses (parent-kill fallback, bounded wait after kill) were wrong and are removed; KillTree is the plan's simple form
[I] Decision: a process killed by the launcher reports ExitCode 137 on every platform (Unix SIGKILL value); WaitAsync returns -1 only for a timeout (plan contract). Known test Cancel_KillsProcessTree_Within3Seconds is unchanged
[I] New test Cancel_KilledProcess_ReportsExitCode137OnEveryPlatform pins this
[I] Windows: Start_NotExecutable test returns early (the execute bit is a Unix concept)

### R07 Locator, FFmpeg info result, platform detection
Result: DONE. CI run 42562529548 (3591bee): ubuntu and macos green; windows fails only on the 2 PlannerTests assigned to R10; all 8 R07 Windows failures now pass (PR #7)
[V] Services/Models/Console build Release 0 warnings/0 errors; dotnet format exit 0 (Models, Services, Console)
[V] `grep -rEn "Process\.Start\(|new ProcessStartInfo" src` outside Processes/ProcessLauncher.cs -> 0 matches (PlatformDetector no longer spawns `bash -lc`)
[V] Behavior runner (23 checks, FakeExe wrappers): info 6.1.1 ok; 5.1 -> TooOld; master N-... not too old; exit 127 -> "exit code 127"; unreadable output; missing file -> "file not found"; non-executable -> "cannot be started"; user cancel propagates; locator argument/env authoritative; empty and relative PATH entries ignored; WinGet Links and Gyan glob (os: Windows); KnownFolders Linux order and Windows; PathFolders; platform detection with Windows extensions
[V] Binary: `--ffmpeg-path /nope/ffmpeg -l` and `DRAWPLATE_FFMPEG=/bad/ffmpeg -l` -> `E-FF-02 ... (file not found)`, exit 3; `--ffmpeg-path /bin/true -l` -> E-FF-02 `(unreadable version output)`, exit 3; `-h` with empty PATH exit 0
[V] Real FFmpeg 6.1.1 at /usr/bin/ffmpeg is found through Linux known folders with PATH=/nonexistent and passes the info step
[V] New/changed tests compile against the xunit stub (LocatorTests, InfoServiceTests, PlatformTests, ArchitectureTests, VersionTests)
[V] Runner found a bug that is now fixed and pinned by InfoService_UserCancel_Propagates: after a user cancel the launcher kills FFmpeg and WaitAsync returned normally, so the service reported "exit code 137" instead of propagating the cancellation (fixed with ct.ThrowIfCancellationRequested after the wait)
[I] Plan said `FfmpegInfoResult` already existed; only FfmpegNotRunnable and FfmpegTooOld did, so it was added
[I] Deviation: E_FF_07 did not exist in Messages; added with my own wording ("FFmpeg {version} at {path} is too old; Drawplate needs 6.0 or newer." / hint "Update FFmpeg, or choose a newer one with --ffmpeg-path."), exit 3
[I] Deviation: PATH parsing is in new Platform/PathFolders.cs (not in FfmpegLocator); Locate got an optional `os` parameter so the Windows glob is tested on every OS; the two skipped Windows locator tests are now real tests
[I] Deviation: FfmpegLocator.EnumerateDirectories added because Directory.GetDirectories throws when WinGet is not installed
[I] Windows fixes in tests: InfoServiceTests uses a .cmd wrapper on Windows (was a #!/bin/sh script + chmod); VersionTests uses drawplate.exe on Windows; LocatorTests.Path_Found builds paths with Path.Combine. Target: the 8 known Windows failures assigned to R07 (VersionTests x3 [-V, --version, UnknownOption], Path_Found, InfoServiceTests x4)
[I] The earlier note "R07 (6)" was a miscount: R07 has 8, R06 had 1 (resolved), R10 has 2
[I] Known interim state from R05: on a machine with FFmpeg, normal runs reach EncoderProbe, which still calls FfmpegArgv.Probe(kind) without a VaapiTarget; Linux then reports "Unexpected error: Value cannot be null (Parameter 'vaapi')" (exit 1). resolved in R08 (probe now finds the render node)
[I] InfoServiceTests.Timeout_IsNotRunnable stays skipped: it would take the full 10 s timeout; the launcher timeout is covered by LauncherTests

### R08 Encoder probing
Result: DONE. CI run 42563521517 (bd47b24): ubuntu and macos green; windows fails only on the 2 PlannerTests assigned to R10 (PR #8)
[V] Services and Console build Release 0 warnings/0 errors; dotnet format exit 0
[V] Behavior runner (13 checks, FakeExe as ffmpeg): unlisted hardware encoder starts no process; VAAPI without render nodes -> NoDevice, no process; renderD129 passes after renderD128 fails (3 processes, nodes sorted); standard VAAPI fails then low-power passes (2 processes); passing VaapiTarget recorded; `kinds=[Cpu]` starts exactly one probe and logs one argv line; hung probe killed at the 1 s timeout (1021 ms); Cpu without libx264 starts no process; start failure returns a failed result; cancel throws; the 3 stderr fixtures all fail without throwing
[V] Binary against real FFmpeg 6.1.1 (no GPU, no /dev/dri): `drawplate -l` prints `cpu`, exit 0, 0.4 s total; FFmpeg lists h264_nvenc, h264_qsv, h264_vaapi, so NVENC and QSV were probed and failed fast and VAAPI was skipped as NoDevice
[V] The interim crash from R05/R07 (`Value cannot be null (Parameter 'vaapi')` on Linux with FFmpeg) is gone
[V] New tests compile against the xunit stub (EncoderProbeTests, InfoServiceTests, FakeExeHarness)
[I] Deviation: EncoderProbe has a third optional ctor parameter `listDirectory` (default VaapiDevices.ListDirectory) so tests can inject render nodes; the plan named only probeTimeout and log
[I] Deviation: FakeExeHarness.CreateWrapper() added and InfoServiceTests now uses it (the wrapper logic was duplicated otherwise)
[I] Fixtures hold exactly the captured lines from plan.md Part 3 (not fuller FFmpeg logs, which the plan does not contain): nvenc-no-libcuda.txt, qsv-mfx-session.txt, vaapi-no-display.txt
[I] FailureClassifier is unchanged (plan: only if a fixture needs it). The three captures classify as Unknown; the tests require Passed=false and no exception only. B28 stays open for N-GPU-02 classification
[I] Probe log format: `probe <id>: <argv>` per run and `probe <id> failed: <stderr tail>` per failure; the Console passes no logger yet (R11 wires --verbose)
[V] CI first failed to build (run 42563189431): CA1822 on the private nested Rig.Probe; fixed by a static helper. Lesson: CA1822 is enforced for members of private nested types, so my local filter must not hide it there

### R09 Media probing without ffprobe
Result: DONE. CI run 42617881950 (d02f754): ubuntu and macos green; windows fails only on the 2 PlannerTests assigned to R10 (PR #9). First CI run found a Windows-only path bug in my sibling test (GetDirectoryName turns / into \), fixed by building paths with Path.Combine
[V] Services and Console build Release 0 warnings/0 errors; dotnet format exit 0
[V] MediaProbeTests (7 tests) compile against the xunit stub
[V] Behavior runner (7 checks, FakeExe): banner video+audio -> 12.5 s, hasVideo true; audio-only -> hasVideo false; Duration N/A -> null; ffprobe present wins over the banner (7.25 instead of 12.5); ffprobe that cannot start falls back to the banner; FfprobeLocator sibling and none
[V] Real FFmpeg, no ffprobe installed: a 3 s video+audio clip -> 00:00:03, hasVideo=True; a 2 s audio-only m4a -> 00:00:02, hasVideo=False
[I] Deviation: AppHost.cs was changed (one line) to construct MediaProbe(launcher, ffmpegPath, ffprobe) and find ffprobe; the plan said this belongs to R10, but the new constructor signature requires it to compile
[I] Deviation: FfprobeLocator added as a separate file (the plan lists it); unit tests cover sibling and none
[I] Fixtures: tests/Drawplate.Tests/Fixtures/banner/{video-audio,audio-only,duration-na}.txt are FFmpeg banner-shaped text written for this stage (the plan named the test but not the fixture files)
[I] ffprobe is invoked with the same argv as before (the plan's queries); a non-zero exit from ffprobe falls back to the banner

### R10 Input discovery and planning wiring
Result: DONE. CI run 42619608587 (a000c5f): ubuntu, macos AND windows all green (first fully green run on all three OS). PR #10
[V] Models, Services, Console build Release 0 warnings/0 errors; dotnet format exit 0
[V] Binary with real FFmpeg 6.1.1 (planning before FFmpeg is located): `-o <file>` -> E-USAGE-08 exit 2; `x/clip.mp4 y/clip.mov` -> E-USAGE-09 exit 2 with both groups listed; `notes.txt` alone -> E-RUN-01 exit 1; output equals input with one mp4 -> all skipped, exit 0; `-i <folder>` with a real 3 s clip -> converted, exit 0, default output `<folder>/compressed`; file mode with a real clip -> `compressed` in the current folder, exit 0; `-i` with file names -> E-USAGE-11 exit 2
[V] R03 REGRESSION found and fixed here: positional files were stored only in ParsedCommand.Files, never in RawOptions.Files, so file-mode runs fell through to scanning the current folder (E-RUN-01). Also RawOptions.InputFolder defaulted to "." which made the "-i with files" check fire on every run. Both fixed; the R03 tests did not catch it because they checked ParsedCommand
[V] Planner fix (product bug, any OS): sources were compared to the output folder without canonicalizing, so a relative source inside the output folder would be converted again. Sources are now canonicalized with fs.FullPath. This is also the Windows PlannerTests fix (the fake file system keys are full paths now)
[I] Deviation: AppHost.Run takes (args, stdout, stderr, ct) (CancellationToken last, CA1068). Flow tests use it in-process. PlainRenderer still writes to Console; R12 moves it to the injected writers
[I] Deviation: planning runs before FFmpeg is located, so usage and collision errors start no process; the plan does not say the order
[I] N-01 prints `- <name> (unsupported file type)` on stdout; the skip glyph comes with the renderer rework (R11/R12)
[I] `-e <encoder>` pinning is not applied in this stage (ConvertOptions.Encoder is validated but the chain is still all passed encoders). EncoderChain is R11
[I] InputDiscovery returns full paths (GetFullPath) and only regular files; a subfolder named x.mp4 is ignored
[I] Windows failures of the R10 PlannerTests are fixed by the planner canonicalization and a normalized fake file system. CI on windows-latest is the proof

### R11 Encoder selection, -l, hardware note, plan card
Result: DONE. CI run 42712294490 (86273e7): ubuntu, macos, windows all green. Fixes during CI: OS-specific encoder names in tests, GpuDetector now catches SecurityException (product bug found on Windows)
[V] Models, Services, Console build Release 0 warnings/0 errors; dotnet format exit 0
[V] Real binary with FFmpeg 6.1.1 (no GPU, no /dev/dri): `-l` prints `cpu`, exit 0, no input needed; `-c` plan card `Encoder [CPU]`, no note; `-e nvenc` -> `Encoder 'nvenc' is not working on this machine.` with hint, exit 1; `-s hd` plan card `Max size hd 1280x720`; default chain `[CPU]`, no note (no GPU)
[V] EncoderFlowTests (6 tests) compile against the xunit stub; FakeExe answers probes by encoder name
[I] GpuDetector gets a test seam (static Override); AppHost is unchanged in how it asks for the GPU
[I] DEFERRED: Flow_NvencFails_VaapiPasses_ChainVaapiCpu and the VAAPI parts of the chain need render nodes; CI runners have none. They need a VaapiDevices listing seam threaded through AppHost (not done)
[I] Plan card shows the encoder chain via badges; the summary still prints the chain where the old code printed `[CPU] x1` (the summary is rebuilt in R12)
[I] Summary line still shows output size 0 B; that is R12 (renderer and conversion loop)
[I] AppHost recovered from a bad regex edit that dropped the WorkPlan/PlanWork/Convert tail; restored from the R10 text with the R11 changes. Verified by build and by the binary scenarios above

### R12 Conversion loop, renderer, summary, --verbose
Result: DONE. CI run 42924235568 (f0af20c, before squash): ubuntu, macos, windows all green; each shows Build 0 Warning(s)/0 Error(s) and `Passed: 327, Failed: 0, Skipped: 1`. PR #12
[V] No .NET SDK in the authoring sandbox (download blocked by the proxy); GitHub Actions on the PR was the gate. Three red CI cycles on the way: CA1859 (private methods must take the concrete PlainRenderer) and xUnit2012 in a new test; both fixed, no suppressions
[V] Tests written first and pushed as a separate commit (075e8bc, compile-red because the APIs did not exist); then the implementation. The red run itself did not execute (no CI trigger on plain pushes), so "failed for the right reason" is the compile error, not a test failure
[V] New tests: PlainRendererTests (9, golden text from D.4, both glyph sets), RunnerTests (4), ConversionFlowTests (5)
[I] Deviation: `tests/FakeExe/Program.cs` gained a `content` property for `createFile` (a fake conversion must write a non-empty file; the runner rejects empty output). The stage lists no FakeExe change
[I] Deviation: skip reason text is `output already exists` (plan Part 7 D.4 and E), not `already exists` as the R12 text says; the two sources disagree and Part 7 is binding
[I] Deviation: the `Speed` row of the summary (`N.Nx realtime`) is not printed; it needs the media duration per file and `FileResult` has none (FileResult.cs is outside the stage's file list). Proposal: add `MediaDuration` to FileResult in a later stage
[I] Deviation: the 30-minute process timeout on conversions was removed (a long video would have been killed); cancellation still kills the tree. Needs the owner's OK
[I] Numbered list N includes files skipped as `output already exists` or `already in the output folder`; W-01, W-02 and N-01 lines are unnumbered. Plan card `Files` counts the numbered files
[I] VAAPI without a probed render node is skipped for that file and the next chain entry is tried; the Vaapi path is only exercised with FakeExe (no render node on CI): [U] on real hardware until R20
[U] Not run: real FFmpeg conversion through the CLI (R16), cancellation (R13)

### R13 Signals and cancellation
Result: DONE. CI run 42928618632 (before this docs commit): ubuntu, macos, windows green; ubuntu and macOS `Passed: 332, Failed: 0, Skipped: 1`, windows `Passed: 327, Skipped: 6` (the 5 Unix-only signal tests skip there). PR #13
[V] Tests (UnixFact, drawplate process signalled by PID only while a FakeExe conversion hangs): Sigterm_ToPidOnly_Exit130Within5s, Sigint_Exit130, Sighup_Exit130, Cancel_DeletesPartial, Cancel_NoChildLeft (pgrep for the unique temp folder). All pass on ubuntu and macOS
[V] CI found a real bug on the first run: all signals gave exit 1 with empty stderr. A killed ffmpeg is an ordinary failed file for the runner, so the loop carried on and the host finished normally. Fixed in AppHost by re-checking the token after each file (`ThrowIfCancellationRequested`)
[I] Deviation: new test helper file `tests/Drawplate.Tests/Cli/UnixFactAttribute.cs` (CA1034 forbids a nested attribute)
[I] SIGQUIT is registered but has no test; the plan's test list has none
[U] Windows: Ctrl-C and Ctrl-Break delivery is not tested (signal tests skip there). Needs the R17 CI work or a manual check


### R14 Setup, doctor, interactive menu, install flags
Result: DONE.
[I] Implemented FFmpeg acquisition with nullable path handling, injected output writers, platform install guidance, and post-install relocation/revalidation. The flow covers no-install, assume-yes, interactive accept/decline, non-interactive, missing installer, failed install, and post-install lookup paths.
[I] Implemented asynchronous doctor diagnostics for FFmpeg source/version, libx264, optional ffprobe, working encoders, and hardware notes. Doctor exits 3 when neither libx264 nor a hardware encoder is usable.
[I] Wired `setup`, `doctor`, `-y`, `-n`, and `-m` into AppHost. `-y` is setup-only and exits after setup; interactive mode probes all available encoders, lets menu choices override pinned options, and runs the menu before input planning.
[I] Added acquisition, relocation, doctor, menu, setup CLI, and no-elevation acceptance tests. Locator and ffprobe seams make the failure and optional-dependency cases deterministic.
[V] CI run 42959324706 (commit 220ba83): Ubuntu, macOS and Windows build, format, test, publish, and binary verification all passed.
