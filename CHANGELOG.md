# Changelog

All notable changes to Drawplate are documented in this file.

## [Unreleased]

### P0 - Baseline and repository
- LICENSE (MIT), README.md stub, AGENTS.md, PROGRESS.md
- docs/PROVENANCE.md, docs/VNEAT-BASELINE.md, docs/decisions/0001-ffmpeg-acquisition.md

### P1 - Scaffold and guardrails
- Solution and build configuration (.NET 10, Native AOT, LangVersion latest, nullable, TreatWarningsAsErrors)
- Entry point stub (--version / -V, ExitCode)
- Architecture tests enforcing project dependency rules
- CI workflow (build, test, publish on ubuntu-latest, macos-latest, windows-latest)

### P2 - Core domain (Models; no IO)
- QualityScale (CRF mapping 51 - q/2) (F07)
- SizeCaps (sd/hd/fullhd/4k, even-scale filter) (F09, F10)
- Encoder sets, profiles, FfmpegArgv golden vectors (F07, F08, F11, F14)
- ProgressParser (-progress pipe:1 parsing) (F15)
- InputRules, OutputNaming, ConversionPlanner (F01, F02, F04, F05, F06)
- FailureClassifier, Options and validation, Message catalog (E-USAGE)
- Sanitizer, Glyphs, HumanFormat (F27, D.4)
- FfmpegBannerParser (version, encoders, duration, video stream)
- InstallAdvisor (winget/Homebrew/apt/pacman/dnf/zypper suggestions) (F20)

### P3 - Process layer (Services)
- IProcessLauncher, ProcessLauncher, RingBuffer (64 KiB stderr) (T3.1)
- FakeExe scriptable stand-in executable (T3.2)
- Process launcher tests (stdout streaming, stderr ring buffer, timeout, cancel, exit code, args) (T3.3)
- FfmpegLocator (--ffmpeg-path, DRAWPLATE_FFMPEG, PATH, known folders) (T3.4)
- FfmpegInfoService (version, encoders, libx264) (T3.5)
- EncoderProbe (15 s timeout per encoder) (T3.6)
- MediaProbe (ffprobe / ffmpeg banner fallback) (T3.7)
- PlatformDetector, GpuDetector (T3.8)

### P4 - Conversion pipeline and CLI = milestone M1
- InputDiscovery, RealPlanFileSystem (F01, F26)
- ConversionRunner (per-file sequential, encoder fallback, partial + atomic move, timestamps) (F04, F05, F13, F14, F17)
- OptionTable and ArgParser (getopts semantics) (D.1)
- PlainRenderer (plan card, per-file lines, summary) (D.4)
- SignalHandler (SIGINT/SIGTERM/SIGHUP/SIGQUIT, Ctrl-C) (F23)
- AppHost and Program (convert flow, exit codes 0/1/2/3/130) (F01-F22)

### P5 - Terminal UI
- Theme detection (colors, glyphs, width, interactive)

### P6 - Hardware encoders
- EncoderChain (auto / specific, fallback to CPU) (F11, F13)
- HardwareNote (N-GPU-01, N-GPU-02) (F30, D.7)

### P7 - FFmpeg acquisition and doctor
- IPrompt, ConsolePrompt (T7.1)
- InstallRunner (runnable suggestions via winget/Homebrew) (T7.2)
- AcquisitionFlow (install flow per D.5) (T7.3)
- DoctorService (read-only FFmpeg/encoder/hardware report) (F29, D.6)

### P8 - Installer scripts
- scripts/build-installers.ps1 generator
- scripts/src/sh/install.sh
- scripts/src/ps1/install.ps1

### P9 - Interactive menu
- MenuService (encoder / quality / max size prompts) (F19)

### P10 - Release and docs
- CHANGELOG.md, MIGRATION.md, FFMPEG.md, HARDWARE.md stubs
