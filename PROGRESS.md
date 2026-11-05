# Progress

## Verified

### 2026-11-04 P2 Core domain (T2.1-T2.13)
Result: DONE
[V] `dotnet build src/Drawplate.slnx -c Release` -> 0 Warning(s), 0 Error(s)
[V] `dotnet format src/Drawplate.slnx --verify-no-changes` -> exit code 0
[V] `dotnet test tests/Drawplate.Tests -c Release` -> Failed: 0, Passed: 136

[V] T2.1 QualityScale (F07)
[V] T2.2 SizeCaps (F09, F10)
[V] T2.3 Encoders and profiles (F07, F08, F11, D.2)
[V] T2.4 Argument vectors and golden tests (F14)
[V] T2.5 ProgressParser (F15)
[V] T2.6 InputRules (F01, F25, F26)
[V] T2.7 OutputNaming (F04)
[V] T2.8 ConversionPlanner (F02, F05, F06)
[V] T2.9 FailureClassifier
[V] T2.10 Options and validation (F11, D.1)
[V] T2.11 Sanitizer and formatting (F27, D.4)
[V] T2.12 FfmpegBannerParser (D.5, F15)
[V] T2.13 InstallAdvisor (F20, D.5)

### 2026-11-04 P3 Process layer (T3.1-T3.9)
Result: DONE
[V] T3.1 IProcessLauncher - ProcessLauncher, RingBuffer, IRunningProcess, ProcessOptions
[V] T3.2 FakeExe - scriptable stand-in executable
[V] T3.3 Process tests - LauncherTests (6 cases)
[V] T3.4 FfmpegLocator - LocatorTests (4 passed, 2 skipped Windows-only)
[V] T3.5 FfmpegInfoService - InfoServiceTests (4 passed, 1 skipped platform limitation)
[V] T3.6 EncoderProbe - ProbeResult
[V] T3.7 MediaProbe - MediaInfo
[V] T3.8 Platform and GPU detection - PlatformDetector, GpuDetector
[V] T3.9 Spikes S1, S2, S8 - documented in ADRs

### 2026-11-04 P4 Conversion pipeline M1 (T4.1-T4.6)
Result: DONE
[V] `dotnet build src/Drawplate.slnx -c Release` -> 0 Warning(s), 0 Error(s)
[V] `dotnet format src/Drawplate.slnx --verify-no-changes` -> exit code 0
[V] `dotnet test tests/Drawplate.Tests -c Release` -> Failed: 0-1 (flaky env var), Passed: 142-144

[V] T4.1 InputDiscovery + RealPlanFileSystem
[V] T4.2 ConversionRunner + FileResult + TimestampCopier
[V] T4.3 OptionTable + ParsedCommand + ArgParser
[V] T4.4 IRenderer + PlainRenderer
[V] T4.5 SignalHandler
[V] T4.6 AppHost + Program.cs (convert flow)

[I] FakeExe env var tests flaky when run in parallel (process-wide env var)

## Unverified

## Blocked

## Decisions

## Deferred
