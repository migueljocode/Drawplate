# Drawplate: agent execution plan

**Status:** DRAFT until the owner writes `APPROVED <date>` in the status table (section S). This file is the single source of truth for scope, behavior and tasks. The research report explains why; this file says what to do.
**Product:** Drawplate, a cross-platform command-line tool (Native-AOT .NET) that converts batches of videos to H.264/AAC MP4 with FFmpeg. It replaces the `vneat` scripts (`Bash/vneat`, `Zsh/vneat.zsh`, `PowerShell/vneat.ps1`) in `migueljocode/WorkWithIO`.
**Reference repos (read-only):** `migueljocode/AriaSharp` (conventions, CI, release), `migueljocode/WorkWithIO` (the scripts being replaced).

## How to read this file (context budget)

Never load the whole file into the agent. For every task load: **section A**, **section C**, the **task** itself, and only the sections the task cites (D, E, G, H, I). Index: A agent prompt, B facts and scope, C conventions, D behavior specification, E message catalog, F tasks P0 to P10, G installer specification, H README specification, I appendices, S status.

## S. Status

| Phase | State | Owner approval |
|---|---|---|
| P0 Baseline and repo | [x] | APPROVED 2026-11-04 |
| P1 Scaffold and guardrails | [ ] | [ ] |
| P2 Core domain | [ ] | [ ] |
| P3 Process layer, locator, detection | [ ] | [ ] |
| P4 Conversion pipeline and CLI = milestone M1 | [ ] | [ ] |
| P5 Terminal UI | [ ] | [ ] |
| P6 Hardware encoders and the hardware note | [ ] | [ ] |
| P7 FFmpeg acquisition and doctor | [ ] | [ ] |
| P8 Installer scripts | [ ] | [ ] |
| P9 Interactive menu | [ ] | [ ] |
| P10 Release and docs | [ ] | [ ] |

Order: P0, P1, P2, P3, P4 (M1), then P5 to P9 in any order the owner picks (P6 before P7 is recommended), then P10. Do not start a phase before its predecessor is approved.

## A. Agent operating prompt (paste this block as the first message of every agent session)

```
You are the implementation engineer for the project "Drawplate". Your job is to execute ONE task from plan.md at a time, exactly as written, and to prove each result with commands you actually ran.

SOURCES OF TRUTH, in order: (1) the plan.md task you were given and the sections it cites, (2) AGENTS.md, (3) PROGRESS.md. If two sources disagree, or the task is ambiguous, STOP and ask. Never guess and never choose silently.

NON-NEGOTIABLE RULES
1. Scope: do only what the task lists. No extra features, refactors, renames, dependencies, or files outside the task's file list. If more seems necessary, STOP and report it as a proposal.
2. Evidence: never claim a command, test or CI run succeeded unless you ran it in this session and saw the result. Label every claim [V] verified (you ran it), [I] inferred, or [U] unknown.
3. Facts: never invent package versions, API names, URLs, hashes, flags or file contents. Look them up (package search, --help, source code) or write [U] and ask. Never type a SHA-256 from memory.
4. Tests first: write the tests listed in the task, run them, see them fail for the right reason, then implement until they pass. Never weaken, skip, delete or rename a test to make it pass.
5. Warnings are errors. No NoWarn, no pragma disable, no analyzer suppression. Fix the cause.
6. Architecture: Models has no packages, no IO, no processes. Services never references Spectre.Console. Only Console references Spectre. Do not edit the architecture test to allow a violation.
7. Processes: always ProcessStartInfo.ArgumentList. Never build a command-line string. Never use a shell (sh -c, cmd /c, powershell -Command with interpolated data).
8. No elevation: never call sudo, pkexec, runas, or install system packages. Linux install commands are only printed for the user to run.
9. Files: never overwrite or delete files you did not create in this run. Partial files are unique per process and always cleaned up.
10. PowerShell: scripts must run on Windows PowerShell 5.1 and PowerShell 7+. ASCII only. No PowerShell-7-only syntax.
11. Style: follow section C exactly.
12. User-facing text: use only the strings in sections D and E. Do not rephrase them.
13. Commits: one commit per task, format in section C.9. Never commit binaries, media files, secrets or build output.
14. Security: file names are untrusted input. Never interpolate them into shell strings. Sanitize before printing.
15. Waiting: no arbitrary sleeps to hide races. Use the waiting primitives named in the task.

WORKING LOOP (for the single current task)
1. Read the task. Restate it in three lines: goal, files, done criteria. If your tool has a plan mode, wait for approval.
2. Check the preconditions under "Depends". If one is not met, STOP.
3. Write the failing tests. Run them. Record the failure message.
4. Implement in small steps. After each file run the build; it must show 0 warnings and 0 errors.
5. Run the task's Verify commands exactly as written and compare with Expected.
6. Self-review with the checklist in section C.10. Fix and re-run.
7. Update PROGRESS.md using the template in section I.6.
8. Commit once.
9. Report in the REPORT FORMAT.
10. STOP. Do not start the next task until told.

STOP AND ASK WHEN: a precondition fails; a Verify output differs from Expected; a test would need weakening; the task needs a file or dependency it does not list; two sources disagree; you are unsure about an API; the same step failed twice; CI is red and the cause is not in your change.

REPORT FORMAT
Task: <id and title>
Result: DONE | BLOCKED
Changed files: <list>
Commands run: <command> -> <exit code and the key output lines> [V]
Tests added: <names>
Deviations: <none, or list with reason>
Inferences and unknowns: [I] ... [U] ...
Proposed next task: <id>
```

## B. Project facts and scope

**What it does.** Converts videos (`mov mp4 mkv avi webm`) from a folder or an explicit file list into `.mp4` (H.264 video, AAC 96k audio, `+faststart`) in an output folder, with a quality percentage, an optional resolution cap, hardware encoders when they work and the CPU (libx264) otherwise, live progress, and a summary.

**v1 scope.** Everything in section D. Platforms: linux-x64, linux-arm64, osx-arm64, win-x64 (osx-x64 and win-arm64 later). Stack: .NET 10, C# `LangVersion latest`, Native AOT, xUnit, Spectre.Console (from P5, Console project only). No other runtime dependency.

**Out of scope for v1 (do not build):** installing or updating GPU drivers; downloading, bundling or hosting FFmpeg ourselves (the user installs it; winget and Homebrew are offered); a settings file; recursive folders; parallel conversion; other codecs or containers; HDR tone mapping; subtitles; a GUI; any use of sudo, pkexec or runas.

**Facts you may rely on [V] (measured on Bash `vneat` 2.0.0, Linux, ffmpeg 6.1.1, 31 Oct 2026):**
- Quality q maps to CRF `51 - floor(q/2)`: q 100, 99, 75, 50, 25, 1 give CRF 1, 2, 14, 26, 39, 51 (read from the `crf=` tag x264 writes into the output).
- `-s sd` on 1280x720 gives 852x480; on 720x1280 gives 480x852; 160x120 is never upscaled; 321x241 becomes 320x240.
- A file with a supported extension but no video stream fails inside ffmpeg with `Error opening output files: Invalid argument`.
- Two inputs that map to the same output name (`x/clip.mp4`, `y/clip.mov`) silently skipped the second one in vneat. Drawplate must refuse instead (F06).
- SIGTERM sent only to the vneat process was ignored while ffmpeg ran. Drawplate must stop within 5 seconds (F23).
- `ffmpeg -hide_banner -i <file>` prints `Duration: HH:MM:SS.xx` and `Stream #0:N...: Video: ...` lines to stderr.
- `-progress pipe:1` prints `key=value` lines; `out_time_us` and `out_time_ms` both hold microseconds; a block ends with `progress=continue` or `progress=end`.
- winget package `Gyan.FFmpeg` 9.0.2: portable zip (no installer), command aliases `ffmpeg`, `ffplay`, `ffprobe`; the manifest SHA-256 matched the real download; the zip is 245.8 MiB and each exe is about 217 MiB unpacked; `ffmpeg.exe` contains libx264, h264_nvenc, h264_amf, h264_qsv (string search, not executed).
- Homebrew's `ffmpeg` formula builds with `--enable-gpl`, `--enable-libx264` and VideoToolbox.
- Ubuntu 26.04 package names that exist: `ffmpeg`, `vainfo`, `intel-media-va-driver`, `intel-media-va-driver-non-free`, `libvpl2`, `libmfx-gen1.2`. These do not exist there: `mesa-va-drivers`, `libmfx1`, `libva-utils`.
- FFmpeg's NVENC message when the driver is too old: `Driver does not support the required nvenc API version. Required: X Found: Y`, followed by a line naming the minimum driver version.

**Environment prerequisites (check before P1):** .NET SDK matching `global.json`; git; `gh` CLI; FFmpeg 6.0 or newer on PATH (integration tests); on Windows both `powershell.exe` (5.1) and `pwsh` (7+), Pester 5.x, PSScriptAnalyzer; on Linux and macOS `shellcheck`.

## C. Engineering conventions

**C.1 Repository layout**

```
AGENTS.md  PROGRESS.md  plan.md  README.md  LICENSE  THIRD-PARTY-NOTICES.md  CHANGELOG.md
global.json  Directory.Build.props  Directory.Packages.props  .editorconfig  .gitignore
src/Drawplate.slnx
src/Drawplate.Models/      pure domain
src/Drawplate.Services/    IO, processes, platform
src/Drawplate.Console/     CLI, UI, composition root
tests/Drawplate.Tests/     Unit/ Process/ Integration/ Cli/ Architecture/ Golden/ Fixtures/
tests/FakeExe/             scripted stand-in executable
scripts/src/sh/  scripts/src/ps1/  scripts/dist/  scripts/tests/  scripts/build-installers.ps1
docs/  VNEAT-BASELINE.md FFMPEG.md HARDWARE.md MIGRATION.md PROVENANCE.md decisions/
.github/workflows/  ci.yml  installers.yml  release.yml
```

**C.2 Dependency rules.** Models references nothing but the BCL. Services references Models. Console references Models, Services and Spectre.Console. Tests reference all three. These rules are enforced by the architecture test (T1.3).

**C.3 Code style** (the owner's standing preference: minimal, modern, small files with one reason to change)
- `LangVersion latest`, nullable enabled, file-scoped namespaces, one top-level type per file named after the file.
- Each project has `GlobalUsings.cs` containing only `global using` lines. Never put a project's own namespace in `Directory.Build.props`.
- Prefer `record` and `record struct`, primary constructors, collection expressions (`[a, b, ..c]`), switch expressions, pattern matching (`is >= 1 and <= 100`), `required`/`init`, raw string literals, `ImmutableArray<T>` for public collections.
- No `static` mutable state. Time through `TimeProvider`; processes through `IProcessLauncher`; file system access through the abstractions the task names.
- Public API is `internal` by default; `public` only where another project needs it (use `InternalsVisibleTo` only for the test project).
- Comments explain *why* and carry matrix ids (for example `// F06`); no commented-out code.

**C.4 Error handling.** Expected failures are values (result records, enums such as `FailureKind`). Exceptions are for programmer errors (`ArgumentException`), cancellation (`OperationCanceledException`) and unexpected IO at the host boundary. `catch (Exception)` is allowed only in `AppHost` (maps to exit 1 with the message) and in best-effort cleanup (comment why). Never swallow cancellation.

**C.5 Output rules.** stdout: plan card, per-file lines, summary, `--list-encoders`, `doctor`, notes. stderr: warnings and errors. ANSI colors only when stdout is a terminal and `NO_COLOR` is unset. Unicode glyphs only when the console output encoding is UTF-8 and `TERM` is not `dumb`; otherwise the ASCII table in D.4. Width from the console (80 when unknown). Live progress only on a terminal. Every printed file name goes through `Sanitizer.ForDisplay` (strips control characters and ESC).

**C.6 Process rules.** One launcher (`IProcessLauncher`, T3.1). Arguments are lists. stdout is read line by line; stderr goes to a 64 KiB ring buffer. Every probe has a timeout. Cancellation kills the whole process tree. Tests use `FakeExe`, never real `sudo`, `winget` or `brew`.

**C.7 Filesystem, culture, time.** `CultureInfo.InvariantCulture` for every parse and format. Paths with `Path.Combine`/`Path.GetFullPath`. Collision and ordering comparisons use `StringComparison.OrdinalIgnoreCase` (never `ToLower()`); ordering falls back to `Ordinal`. UTF-8 without BOM for every file we write. Resolve symlinks before comparing paths.

**C.8 Testing.** xUnit. Name: `Subject_Condition_Expected`. Tier trait on every test: `[Trait("Tier","T0")]` unit, `T1` process (FakeExe), `T2` integration (real ffmpeg), `T3` artifact smoke, `T4` static. No network in T0 to T3. Integration tests use `[FfmpegFact]`, which **fails** when FFmpeg is missing and the environment variable `CI` is set, and skips with a message locally. Temp folders come from `TempDir` (deleted on dispose). Media is generated at test time (I.2) and never committed. Golden argument vectors live in `tests/Drawplate.Tests/Golden/*.txt`, one argv element per line, with `SRC` and `PARTIAL` placeholders.

**C.9 Git.** Branch `task/<id>-<slug>`. One commit per task, Conventional Commits: `feat(models): add QualityScale (F07)`; scopes `models services console tests ci scripts docs`; the body lists the Verify commands run. Never force-push shared branches. PR title equals the commit subject.

**C.10 Self-review checklist (run before reporting).** (1) Only listed files changed. (2) Build has 0 warnings. (3) New tests fail without the implementation and pass with it. (4) No suppressions. (5) No string-built command lines. (6) No new dependency. (7) User-facing strings match D and E exactly. (8) Matrix ids are in comments where rules are non-obvious. (9) No leftover debug output or TODO. (10) PROGRESS.md updated with [V]/[I]/[U].

**C.11 AOT rules.** No reflection, no `Assembly.Load`, no `dynamic`. JSON only through a `JsonSerializerContext`. `IsAotCompatible` on Models and Services. Trim and AOT warnings are errors. `InvariantGlobalization` true.

**Standard verification commands** (used by name in tasks)
- `STD-BUILD`: `dotnet build src/Drawplate.slnx -c Release` -> Expected: `0 Warning(s)`, `0 Error(s)`.
- `STD-FORMAT`: `dotnet format src/Drawplate.slnx --verify-no-changes` -> Expected: exit code 0. (If `.slnx` is not supported by `dotnet format` in the installed SDK, run it per project and record that in PROGRESS.md.)
- `STD-TEST`: `dotnet test tests/Drawplate.Tests -c Release` -> Expected: `Failed: 0`.
- `TEST <filter>`: `dotnet test tests/Drawplate.Tests -c Release --filter "<filter>"` -> Expected: the named tests are listed as passed and `Failed: 0`.

## D. Behavior specification

### D.1 Command line

```
drawplate [options] [file ...]     convert the given files
drawplate [options] -i <folder>    convert every video in a folder (not recursive)
drawplate                          convert the current folder
drawplate setup [-y] [-n]          show how to get FFmpeg; with consent, run the install command
drawplate doctor                   read-only check of FFmpeg, encoders and hardware
```

`setup` and `doctor` are subcommands only when they are the first argument. To convert a file named `setup`, write `./setup` or put `--` before it.

| Short | Long (aliases) | Argument | Default | Rule |
|---|---|---|---|---|
| `-i` | `--input` (`--input-dir`) | folder | `.` | Folder that contains the videos |
| `-o` | `--output` (`--output-dir`) | folder | `<input>/compressed` (folder mode), `./compressed` (file mode) | Created when missing |
| `-t` | `--no-timestamps` | none | dates are copied | Do not copy the original file dates |
| `-q` | `--quality` | 1 to 100 | 50 | Higher is better |
| `-s` | `--max-size` | `sd` `hd` `fullhd` `4k` | none | Case-insensitive |
| `-e` | `--encoder` | name | `auto` | Lowercased; value sets in D.2 |
| `-c` | `--cpu` | none | | Same as `-e cpu` |
| `-l` | `--list-encoders` (`--list`) | none | | Print working encoders, exit 0 |
| `-m` | `--interactive` | none | | Menu; needs a terminal |
| `-y` | `--install-deps` (`--install`) | none | | Run the FFmpeg install command without asking, then exit |
| `-n` | `--no-install` | none | | Never offer or run an install; wins over `-y` |
| `-h` | `--help` | none | | Show help, exit 0 |
| `-V` | `--version` | none | | Print `drawplate <version>`, exit 0 |
| | `--ffmpeg-path` | file | | Use this FFmpeg |
| | `--verbose` | none | | Print the ffmpeg argument list and stderr tails to stderr |

**Parsing rules (getopts semantics, matching vneat).** Short options may be clustered (`-tn`) and may carry an attached value (`-q50`, `-oout`). A value may follow as the next argument (`-q 50`, `--quality 50`) or after `=` for long options (`--quality=50`). `--` ends option parsing. Options are applied left to right; `-V` and `-h` act immediately when reached. After parsing, validate in this order: encoder, quality, max size, then the combination rules.

### D.2 Encoders per operating system

| OS | Values accepted by `-e` | `auto` tries, in order |
|---|---|---|
| Linux | `auto nvenc qsv vaapi cpu` | nvenc, qsv, vaapi, cpu |
| Windows | `auto nvenc qsv amf cpu` | nvenc, qsv, amf, cpu |
| macOS | `auto videotoolbox cpu` | videotoolbox, cpu |

A value that exists on another OS but not here gives E-USAGE-12; a value that exists nowhere gives E-USAGE-04. `cpu` means libx264. Badges printed in output: `[NVENC] [QSV] [VAAPI] [AMF] [VT] [CPU]`.

### D.3 Feature matrix (the contract; each row has acceptance and tests)

| ID | Behavior | Acceptance | Tests |
|---|---|---|---|
| F01 | **Folder mode.** Files in the input folder only (not recursive) with extension `mov mp4 mkv avi webm`, case-insensitive; names starting with `.` skipped; order `OrdinalIgnoreCase` then `Ordinal`. | The fixture folder (I.2) yields exactly `Space Name.MOV`, `bad.mp4`, `odd.avi`, `ok-landscape.mp4`, `portrait.mkv`, `tiny.webm` in that order; `notes.txt`, subfolders and dot-files are ignored. | `InputRules_*`, `InputDiscovery_*` |
| F02 | **Explicit files.** Positional arguments. Missing file: W-01 and skip. Directory: W-02 and skip. Unsupported extension: N-01, counted as skipped. Nothing left: E-RUN-01, exit 1. `-i` together with file names: E-USAGE-11, exit 2. | Each case prints exactly the catalog text. | `Discovery_ExplicitFiles_*` |
| F03 | **Output folder.** Defaults in D.1. Created before work starts. A path that exists as a file: E-USAGE-08, exit 2, nothing converted. | `-o <file>` exits 2 and writes nothing. | `Output_PathIsFile_*` |
| F04 | **Naming and atomic output.** Target `<stem>.mp4`. ffmpeg writes `.<stem>.<pid>.partial.mp4` in the output folder (`-y` applies to the partial only). On success `File.Move(partial, target, overwrite: false)`. The partial is removed on failure and on cancel. | After any failure or cancel no `.partial` file remains; two concurrent runs never share a partial name. | `Runner_Partial_*`, `Parallel_TwoRuns_*` |
| F05 | **Skips.** Target already exists: skip, reason `output already exists`. Source inside the output folder (symlinks resolved): skip, reason `already in the output folder`. Skips are counted and do not change the exit code. | Re-running the same command converts nothing and exits like the first run minus the conversions. | `Planner_Skip*`, `Rerun_*` |
| F06 | **Collisions.** If two files to convert map to the same target name (`OrdinalIgnoreCase`), print E-USAGE-09 and exit 2 before any work. (vneat silently skipped the second file.) | `x/clip.mp4` plus `y/clip.mov` exits 2 and starts zero ffmpeg processes. | `Planner_Collision_*` |
| F07 | **Quality.** Integer 1 to 100, default 50. CRF equivalent `51 - q / 2` (integer division). Used as `-crf` (CPU), `-cq` (NVENC), `-global_quality` (QSV, VAAPI), `-qp_i` and `-qp_p` (AMF). | q 100, 99, 75, 50, 25, 1 give 1, 2, 14, 26, 39, 51. | `QualityScale_*` |
| F08 | **VideoToolbox quality** uses q itself as `-q:v` (not the CRF mapping). | q 50 gives `-q:v 50`. | `EncoderProfiles_VideoToolbox_*` |
| F09 | **Size cap.** Boxes sd 854x480, hd 1280x720, fullhd 1920x1080, 4k 3840x2160; portrait swaps width and height; aspect ratio kept; never upscaled. Exact filter in I.1. | 1280x720 with `sd` gives 852x480; 720x1280 gives 480x852; 160x120 unchanged. | `SizeCaps_*`, `Integration_SizeCap_*` |
| F10 | **Even dimensions.** Every output has even width and height (`scale=trunc(iw/2)*2:trunc(ih/2)*2`). | 321x241 gives 320x240. | `Integration_OddSize_*` |
| F11 | **Encoder option.** Value sets and priority in D.2. `-c` equals `-e cpu`. | `-e amf` on Linux exits 2 with E-USAGE-12. | `Options_Encoder_*` |
| F12 | **Probing.** Each encoder in the OS set is probed, in priority order, with the probe vector (I.1), timeout 15 s. A probe passes on exit code 0. `cpu` passes only when `libx264` is listed by `-encoders`. | A hanging fake probe is killed at 15 s and reported failed. | `Probe_*` |
| F13 | **Fallback chain.** Chain = working encoders in priority order (for `-e X`: X then cpu when available). Per file: try each until one succeeds. The summary counts the encoder that actually produced each file. | First encoder exits 1, second succeeds: file converted, counted under the second. | `Runner_Fallback_*` |
| F14 | **Streams and argument vector.** Exactly the vector in I.1. A file with no video stream fails before ffmpeg runs with reason `no video stream`. | Audio-only `.mp4` fails with that reason, exit 1. | `Argv_Golden_*`, `Integration_AudioOnly_*` |
| F15 | **Progress.** Parse `-progress pipe:1`. Prefer `out_time_us`; use `out_time_ms` only when `out_time_us` is absent; ignore `N/A`. Fraction and ETA only when the duration is known. | Parser tests in T2.5. | `ProgressParser_*` |
| F16 | **Output modes.** Terminal: colors, live two-line progress. Piped or `NO_COLOR`: plain text, no ESC bytes, no live lines. | Output piped to a file contains no byte 0x1B. | `Cli_Piped_NoAnsi` |
| F17 | **Timestamps.** Copy last-access and last-write times from the source to the output unless `-t`; on Windows also the creation time. | Output mtime equals source mtime (to the second). | `Integration_Timestamps_*` |
| F18 | **List encoders (`-l`).** stdout: one encoder id per line in chain order; stderr: any note; exit 0. | On a machine without GPU: `cpu` only. | `Cli_ListEncoders_*` |
| F19 | **Interactive (`-m`).** Needs stdin and stdout terminals, else E-USAGE-10. Prompts, in order: `Encoder` (numbered list, first is `auto`, default 1), `Quality` (1 to 100, default current), `Max size` (`sd`, `hd`, `fullhd`, `4k`, `none`, default current). Invalid input: `Invalid choice.` / E-MENU-01 / E-MENU-02, exit 2. | Scripted stdin tests with a pseudo-terminal abstraction. | `Menu_*` |
| F20 | **FFmpeg acquisition.** Locate FFmpeg (D.5). If missing: show the install command for this OS first (winget on Windows). Offer to run it only where no elevation is needed (winget, Homebrew). `-y` runs it without asking and exits; `-n` never offers. Errors are documented in the README (section H). | Behavior table in D.5. | `Setup_*`, `Advisor_*` |
| F21 | **Environment.** `NO_COLOR`, `DRAWPLATE_FFMPEG`. `VNEAT_NETWORK` is dropped (no internet pre-check exists). | Documented in MIGRATION.md. | `Env_*` |
| F22 | **Exit codes.** 0 success (skips allowed). 1 any file failed, nothing to convert, or an unexpected error. 2 usage or validation error. 3 FFmpeg unavailable and not installed. 130 interrupted. | One test per code. | `ExitCodes_*` |
| F23 | **Cancellation.** SIGINT, SIGTERM, SIGHUP, SIGQUIT (Ctrl-C and Ctrl-Break on Windows): kill the ffmpeg process tree, delete the partial, print N-INT, exit 130 within 5 s. SIGTERM sent to the Drawplate PID alone must work (vneat failed this). | After SIGTERM to the PID: exit 130 within 5 s, zero ffmpeg processes, zero partial files. | `Signals_*` (Unix), `Cancel_*` |
| F24 | **One binary.** The encoder set and probes depend on the OS (D.2). | Same tests run on all three OS. | CI matrix |
| F25 | **Order** of files: `OrdinalIgnoreCase`, then `Ordinal`. | Same order on every OS. | `InputRules_Order_*` |
| F26 | **Hidden files** (names starting with `.`) are skipped in folder mode. | `._clip.mp4` is ignored. | `InputRules_Hidden_*` |
| F27 | **Display names** pass through `Sanitizer.ForDisplay` (control characters and ESC removed). | A name containing ESC prints without it. | `Sanitizer_*` |
| F28 | **No libx264.** If no hardware encoder works and `libx264` is absent: E-FF-03, exit 3. If at least one hardware encoder works: continue without CPU fallback. | Fake `-encoders` without libx264. | `Doctor_NoX264_*` |
| F29 | **doctor.** Read-only. Format and exit codes in D.6. | Fake FFmpeg scenarios. | `Doctor_*` |
| F30 | **Hardware note.** N-GPU-01 or N-GPU-02 rules in D.7. Never a warning, never changes the exit code. | GPU detected plus all hardware probes failing prints exactly one note. | `HardwareNote_*` |
| F31 | **Installer scripts.** Section G. | Section G acceptance list. | `scripts/tests/*` |

### D.4 Output formats

Glyphs (UTF-8 console | ASCII fallback): ok `✓` | `+`; fail `✗` | `x`; skip `–` | `-`; arrow `→` | `->`; down `↓` | `v`; up `↑` | `^`; separator `·` | `.`; bar full `█` | `#`; bar empty `░` | `-`; spinner `⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏` | `|/-\`.
Sizes: 1024-based, units `B KB MB GB`, one decimal except bytes (`122.3 MB` for 128210145 bytes). Times: `m:ss`, or `h:mm:ss` from one hour.

**Plan card** (title `Plan`; labels padded to 9 columns):
```
Input     <folder or "N file(s)">
Output    <folder>
Quality   50 (CRF 26)
Max size  none            (or: hd 1280x720)
Encoder   [NVENC] > [CPU]       (the chain)
Files     N video(s) · 122.3 MB · timestamps kept      (or: not kept)
```
**Per-file lines** (`i` is the 1-based index, `N` the number of files to process):
```
✓ [i/N] <name>  <in> → <out>  ↓32%  [CPU]  0:01        success (↑ when larger)
✗ [i/N] <name>  <reason>                                 failure
– [i/N] <name>  skipped: <reason>                        skip
```
**Live lines** (terminal only, redrawn at most 10 times per second):
```
<spinner> [i/N] <name>  [NVENC]
<bar 20> 42%  1.8x  elapsed 0:12  eta 0:16        (unknown fraction: "--%" and no eta)
```
**Summary card** (title `Summary`): rows `Result` (`N converted   N failed   N skipped`; omit zero failed or skipped), `Size` (`<in> → <out>  ↓ P% smaller` or `↑ P% larger`), a 24-wide bar with `K% of original`, `Saved` (only when positive), `Speed` (`N.Nx realtime`), `Encoders` (`[CPU] x3 [NVENC] x2`), `Time`, `Output`. Final line: `All done.` or `Finished with N failure(s).`
**`--list-encoders`:** stdout only the ids, one per line, for example `nvenc` then `cpu`.

### D.5 FFmpeg acquisition

**Locator order.** (1) `--ffmpeg-path`; (2) `DRAWPLATE_FFMPEG`; (3) PATH; (4) known folders. Windows: `%LOCALAPPDATA%\Microsoft\WinGet\Links`, `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_*\ffmpeg-*\bin` (glob), `%USERPROFILE%\scoop\shims`, `%ProgramData%\chocolatey\bin`. macOS and Linux: `/opt/homebrew/bin`, `/usr/local/bin`, `/home/linuxbrew/.linuxbrew/bin`, `/usr/bin`. Steps (1) and (2) are authoritative: if they point at something unusable, report E-FF-02 and do not fall through.
**Validation.** The candidate runs (`-version` exits 0 within 10 s), reports version 6.0 or newer (E-FF-07 otherwise; master builds named `N-<number>-g<hash>` count as new enough), and `-encoders` is parseable. `libx264` absence is handled by F28.
**ffprobe.** Used when found next to FFmpeg or on PATH; otherwise duration and stream presence come from the `ffmpeg -hide_banner -i <file>` banner. Never required.
**Suggestions by OS** (ordered; exact text in E-FF-01):
- Windows: 1) runnable: `winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements` (needs no administrator rights; portable per-user package). If `winget` is not on PATH show E-FF-05 instead. If `scoop` is on PATH also show `scoop install ffmpeg` (runnable). If `choco` is on PATH also show `choco install ffmpeg` (needs Administrator; shown only, never run).
- macOS: 1) runnable when `brew` is on PATH: `brew install ffmpeg`. Otherwise E-FF-06.
- Linux: printed only, never run (needs administrator rights): apt `sudo apt install ffmpeg`; pacman `sudo pacman -S ffmpeg`; Fedora needs RPM Fusion for libx264 (`sudo dnf install https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm` then `sudo dnf swap ffmpeg-free ffmpeg --allowerasing`); openSUSE `sudo zypper install ffmpeg` with the Packman note. Copy the exact package names from the install functions of `Bash/vneat` (about lines 540 to 620) and check each against the distribution's package index before using it.
**Flow when FFmpeg is missing or unusable**
1. `-n`: print E-FF-01 (or E-FF-02), exit 3, no prompt.
2. `-y` (or `drawplate setup -y`): run the first runnable suggestion without asking; none runnable: print the suggestions, exit 3.
3. Terminal (stdin and stdout): print E-FF-01 with the suggestions; if the first is runnable ask `Run it now? [Y/n] ` (default Y). Y: run it with the console inherited so the package manager shows its own progress; exit code 0: locate again (including known folders); found: continue (convert) or print N-OK-01 (setup); not found: E-FF-04, exit 3. Non-zero exit code: E-FF-08, exit 3. N: exit 3.
4. Not a terminal: print E-FF-01, exit 3.
`drawplate setup` runs steps 1 to 4 even when FFmpeg is already present, but then prints N-SETUP-01 and exits 0 without asking.

### D.6 `doctor`

Read-only. Lines in this order, each `<status>  <check>  <detail>`; status words `ok`, `missing`, `broken`, `outdated`, `note`:
```
ok        FFmpeg     C:\Users\me\AppData\Local\Microsoft\WinGet\Links\ffmpeg.exe (9.0.2, found on PATH)
ok        libx264    present
ok        ffprobe    found (optional)
ok        Encoders   nvenc, cpu
note      Hardware   <N-GPU-01 or N-GPU-02 text without the "Note: " prefix>
```
`FFmpeg`: missing (E-FF-01 text follows), broken (E-FF-02), outdated (E-FF-07). `ffprobe` absent: `ok  ffprobe  not found (optional; durations come from ffmpeg)`. Exit code 0 when FFmpeg is ok (notes never matter), 3 otherwise.

### D.7 Hardware note

`GpuDetector` reports `HasGpu`. After probing: if `HasGpu` and no hardware encoder passed, print N-GPU-01 once (after the plan card when converting, in `doctor`). If a probe for NVENC failed with `FailureKind.NvencDriverTooOld`, print N-GPU-02 instead. If `HasGpu` is false, print nothing. Linux detection: any `/sys/class/drm/card*/device/vendor` equal to `0x10de`, `0x1002` or `0x8086`. Windows: any subkey of the display-adapter class key in the registry (`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}`) whose `MatchingDeviceId` contains `VEN_10DE`, `VEN_1002` or `VEN_8086`, excluding Microsoft Basic Display and Remote Display adapters. macOS: always true.

## E. Message catalog (use these strings verbatim; `{x}` are placeholders; "hint" is printed on the next line, indented two spaces)

| ID | Stream | Exit | Text |
|---|---|---|---|
| E-USAGE-01 | stderr | 2 | `Unknown option '{arg}'.` for long options, `Unknown option -{c}.` for short. Hint: `Run 'drawplate --help' to see all options.` |
| E-USAGE-02 | stderr | 2 | `Option {arg} requires a value.` |
| E-USAGE-03 | stderr | 2 | `Option --{name} does not take a value.` Hint as E-USAGE-01. |
| E-USAGE-04 | stderr | 2 | `Unknown encoder '{value}'.` Hint: `Choose one of: {list}.` |
| E-USAGE-05 | stderr | 2 | `Quality must be a number from 1 to 100 (higher = better).` |
| E-USAGE-06 | stderr | 2 | `Invalid max size '{value}'.` Hint: `Choose one of: sd, hd, fullhd, 4k.` |
| E-USAGE-07 | stderr | 2 | `Input folder not found: {path}` |
| E-USAGE-08 | stderr | 2 | `Output path is a file, not a folder: {path}` |
| E-USAGE-09 | stderr | 2 | `More than one input would write the same output file:` then one line per group `  {output name}: {source}, {source}`. Hint: `Convert them in separate runs or rename one.` |
| E-USAGE-10 | stderr | 2 | `Interactive mode needs a terminal.` |
| E-USAGE-11 | stderr | 2 | `Use either -i <folder> or file names, not both.` |
| E-USAGE-12 | stderr | 2 | `Encoder '{value}' is not available on {os}.` Hint: `Choose one of: {list}.` |
| E-MENU-01 | stderr | 2 | `Invalid choice.` |
| E-MENU-02 | stderr | 2 | `Invalid max size '{value}'.` Hint: `Choose one of: sd, hd, fullhd, 4k, none.` |
| E-TEMP-01 | stderr | 2 | `This option is not available yet.` (temporary; removed by T7.4 and T9.2) |
| E-RUN-02 | stderr | 1 | `Encoder '{value}' is not working on this machine.` Hint: `Run 'drawplate --list-encoders' to see what works.` |
| E-RUN-01 | stderr | 1 | `No supported video files found.` Hint: `Supported formats: mov, mp4, mkv, avi, webm.` |
| W-01 | stderr | | `File not found: {path}` |
| W-02 | stderr | | `Not a file: {path}` |
| N-01 | stdout | | skip glyph, then `{name} (unsupported file type)` |
| N-INT | stderr | 130 | `Interrupted.` |
| E-FF-01 | stderr | 3 | Windows: `FFmpeg was not found.` blank line, `Install it with winget (no administrator rights needed):`, `  winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements`, blank line, `Then open a new terminal and run Drawplate again.`, `Already have FFmpeg somewhere? Use --ffmpeg-path <file> or set DRAWPLATE_FFMPEG.`, `Problems? See "Troubleshooting FFmpeg" in the README.` macOS: the same with `Install it with Homebrew:` and `  brew install ffmpeg`. Linux: `Install it with your package manager (administrator rights are required, so run this yourself):` and the command. |
| E-FF-02 | stderr | 3 | `FFmpeg at {path} could not be run ({reason}).` Hint: `Reinstall it, or choose another with --ffmpeg-path.` |
| E-FF-03 | stderr | 3 | `This FFmpeg build has no libx264 encoder and no working hardware encoder, so nothing can encode.` Hint: Windows `Install the full build: winget install --id Gyan.FFmpeg -e`; macOS `Install it with: brew install ffmpeg`; Linux `See "Troubleshooting FFmpeg" in the README.` |
| E-FF-04 | stderr | 3 | `FFmpeg was installed, but Drawplate cannot find it yet.` Hint: `Open a new terminal and run Drawplate again. Run 'drawplate doctor' to see where it looks.` |
| E-FF-05 | stderr | 3 | `winget was not found.` Hint: `It ships with Windows 10 (1809 or newer) and Windows 11 as "App Installer". Install it from the Microsoft Store, or install FFmpeg manually (see "Troubleshooting FFmpeg" in the README).` |
| E-FF-06 | stderr | 3 | `Homebrew was not found.` Hint: `Install it from https://brew.sh, then run: brew install ffmpeg` |
| E-FF-07 | stderr | 3 | `FFmpeg {version} is older than the supported minimum {min}.` Hint: Windows `Upgrade it: winget upgrade --id Gyan.FFmpeg -e`; macOS `brew upgrade ffmpeg`; Linux `Update it with your package manager.` |
| E-FF-08 | stderr | 3 | `The install command failed (exit code {code}).` Hint: `See "Troubleshooting FFmpeg" in the README.` |
| N-OK-01 | stdout | 0 | `FFmpeg installed.` |
| N-SETUP-01 | stdout | 0 | `FFmpeg is already installed.` |
| N-GPU-01 | stdout | | `Note: no GPU encoder is available, so Drawplate is using the CPU. To enable GPU encoding, install the driver from your GPU vendor.` |
| N-GPU-02 | stdout | | `Note: your NVIDIA driver is older than this FFmpeg's NVENC needs{ (minimum {version})}. Update the driver to enable GPU encoding; Drawplate is using the CPU.` |
| N-VERSION | stdout | 0 | `drawplate {version}` |

Per-file failure and skip reasons (short, single line, clipped to the terminal width): `no video stream`, `invalid or corrupt input`, `encoder failed: {last stderr line}`, `output already exists`, `already in the output folder`.

## F. Tasks

**Task index** (load this list first; then only the task you work on)

- **T0**: T0.1 Repository and documents; T0.2 Owner housekeeping (no agent work); T0.3 Approval; 
- **T1**: T1.1 Solution and build configuration; T1.2 Entry point stub; T1.3 Architecture tests (real); T1.4 CI; T1.5 Agent documents; T1.6 Spike S5: UI library cost; 
- **T2**: T2.1 QualityScale (F07); T2.2 SizeCaps (F09, F10); T2.3 Encoders and profiles (F07, F08, F11, D.2); T2.4 Argument vectors and golden tests (F14); T2.5 ProgressParser (F15); T2.6 InputRules (F01, F25, F26); T2.7 OutputNaming (F04); T2.8 ConversionPlanner (F02, F05, F06); T2.9 FailureClassifier; T2.10 Options and validation (F11, D.1, E-USAGE); T2.11 Sanitizer and formatting (F27, D.4); T2.12 FfmpegBannerParser (D.5, F15); T2.13 InstallAdvisor (F20, D.5, E-FF); 
- **T3**: T3.1 IProcessLauncher; T3.2 FakeExe; T3.3 Process tests (tier T1); T3.4 FfmpegLocator (D.5); T3.5 FfmpegInfoService; T3.6 EncoderProbe (F12); T3.7 MediaProbe; T3.8 Platform and GPU detection; T3.9 Spikes S1, S2, S8 (ADRs); 
- **T4**: T4.1 InputDiscovery and the real file system; T4.2 ConversionRunner; T4.3 OptionTable and ArgParser (D.1); T4.4 PlainRenderer (D.4); T4.5 SignalHandler (F23); T4.6 AppHost and the convert flow; T4.7 Integration fixtures and regression tests (I.2, I.3); T4.8 CI hardening and AOT smoke; T4.9 README quick start and baseline refresh; 
- **T5**: T5.1 Theme detection; T5.2 Cards with Spectre.Console (only if ADR 0003 adopted it); T5.3 Live progress; T5.4 Renderer selection and per-file lines; 
- **T6**: T6.1 Capture golden vectors (manual; owner or an agent with the hardware); T6.2 Encoder chain builder (F11, F13); T6.3 VAAPI profile and device discovery; T6.4 AMF and VideoToolbox profiles; T6.5 Use the hardware chain in the convert flow (F12, F13); T6.6 Hardware note (F30, D.7); T6.7 Hardware checklist (manual); 
- **T7**: T7.1 Prompt abstraction; T7.2 InstallRunner; T7.3 AcquisitionFlow (D.5); T7.4 Wire the flow into the host; T7.5 DoctorService (F29, D.6); T7.6 No-elevation and single-launcher guards (A11); T7.7 README troubleshooting and FFMPEG.md; 
- **T8**: T8.1 Module layout and generator; T8.2 install.sh; T8.3 install.ps1; T8.4 Pester tests and analyzer settings; T8.5 Progress and step display tests; T8.6 Installer CI; 
- **T9**: T9.1 Menu (F19); T9.2 Wire `-m`; 
- **T10**: T10.1 Release workflow; T10.2 Installer end-to-end against a real release; T10.3 Notices and license; T10.4 Changelog and release notes; T10.5 Repository hygiene; T10.6 Migration guide; T10.7 Final README; T10.8 Optional package manifests (owner decision); 

**Task format.** `Depends` = tasks that must be done. `Files` = the only files you may create or change. `Do` = ordered steps. `Tests` = write these first (names are exact). `Verify` = commands and Expected results. `Commit` = the commit subject. After every task: STD-BUILD, STD-FORMAT and STD-TEST must pass (section C), then update PROGRESS.md and report.

### P0. Baseline and repository (no product code)

**T0.1 Repository and documents.** Depends: owner approval of this plan. Files: `LICENSE` (MIT), `README.md` (stub, structure in section H), `AGENTS.md`, `PROGRESS.md`, `.gitignore`, `docs/PROVENANCE.md`, `docs/VNEAT-BASELINE.md`, `docs/decisions/0001-ffmpeg-acquisition.md`.
Do: (1) create the repository `migueljocode/Drawplate` (public) and clone it; (2) `AGENTS.md`: copy the structure of AriaSharp's `AGENTS.md`, then replace its rules with section A rules 1 to 15 verbatim; (3) `PROGRESS.md` with headings Verified, Unverified, Blocked, Decisions, Deferred; (4) `docs/VNEAT-BASELINE.md`: copy section B facts and the matrix D.3; (5) ADR 0001 (template in I.6) recording: winget first, no downloads by Drawplate in v1, drivers are the user's job, no elevation; (6) `docs/PROVENANCE.md` table with columns copied file, source repo, source commit (empty for now).
Verify: `git log --oneline` shows one commit; `ls docs/decisions` lists `0001-ffmpeg-acquisition.md`. Commit: `docs: add baseline, ADR 0001 and agent rules`.

**T0.2 Owner housekeeping (no agent work).** Reserve the GitHub organization handle if wanted, the NuGet package ID if a dotnet tool will be published, and check domain and trademark databases for "Drawplate" (not checked in the research). Record the outcome in `docs/decisions/0002-name.md`.

**T0.3 Approval.** The owner writes `APPROVED <date>` in section S.

### P1. Scaffold and guardrails

**T1.1 Solution and build configuration.** Depends: T0.1. Files: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `src/Drawplate.slnx`, `src/Drawplate.Models/Drawplate.Models.csproj`, `src/Drawplate.Services/Drawplate.Services.csproj`, `src/Drawplate.Console/Drawplate.Console.csproj`, `tests/Drawplate.Tests/Drawplate.Tests.csproj`, `tests/FakeExe/FakeExe.csproj`, one `GlobalUsings.cs` per project.
Do: (1) copy `global.json` and `.editorconfig` from `migueljocode/AriaSharp` and record the source commit in `docs/PROVENANCE.md`; (2) `Directory.Build.props`: `net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings disable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild true`, `Deterministic true`, `ContinuousIntegrationBuild` true when `CI` is `true`, `InvariantGlobalization true`; **no `NoWarn`** and **no global using of a project namespace**; (3) `Directory.Packages.props` with central package management; take the versions of xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk and coverlet.collector from AriaSharp and confirm each exists (`dotnet package search <id> --exact-match`); (4) Models and Services: `IsAotCompatible true`; Console: `AssemblyName drawplate`, `PublishAot true`, `OptimizationPreference Size`, `StripSymbols true`, `TrimmerSingleWarn false`; (5) project references per C.2; Tests references the three projects plus `InternalsVisibleTo` entries; (6) each `GlobalUsings.cs` holds only `global using System.Collections.Immutable;` and `global using System.Globalization;` (Models, Services) or those plus `global using Drawplate.Models;` (Services, Console, Tests).
Verify: `dotnet --version` satisfies `global.json`; STD-BUILD -> `0 Warning(s)`. Commit: `build: add solution and shared build settings`.

**T1.2 Entry point stub.** Depends: T1.1. Files: `src/Drawplate.Models/Cli/ExitCode.cs`, `src/Drawplate.Console/Program.cs`, `tests/Drawplate.Tests/Cli/CliProcess.cs` (helper that starts the built `drawplate` with `ProcessStartInfo.ArgumentList`), `tests/Drawplate.Tests/Cli/VersionTests.cs`.
Do: `ExitCode` enum `Ok 0, Failure 1, UsageError 2, FfmpegUnavailable 3, Interrupted 130`. `Program.Main`: `--version` or `-V` prints N-VERSION using the assembly informational version without the `+commit` suffix, exit 0; any other argument prints E-USAGE-01 and exits 2. (The real parser comes in T4.3.)
Tests: `Version_Flag_PrintsVersionAndExitsZero` (`-V` and `--version`), `UnknownOption_ExitsTwo`.
Verify: `TEST FullyQualifiedName~VersionTests` -> 3 passed. Commit: `feat(console): add entry point stub and ExitCode`.

**T1.3 Architecture tests (real).** Depends: T1.2. Files: `tests/Drawplate.Tests/Architecture/ArchitectureTests.cs`.
Do: use `Assembly.GetReferencedAssemblies()`. Tests: `Models_ReferencesOnlyFrameworkAssemblies` (names start with `System`, `netstandard`, `mscorlib`), `Models_DoesNotReferenceServicesOrConsole`, `Services_DoesNotReferenceSpectreConsole`, `Services_DoesNotReferenceConsole`, `AssemblyReferenceHelper_SeesKnownReference` (the test assembly references `Drawplate.Models`; proves the helper works).
Verify: `TEST FullyQualifiedName~Architecture` -> 5 passed. Commit: `test: enforce project dependency rules`.

**T1.4 CI.** Depends: T1.3. Files: `.github/workflows/ci.yml`.
Do: trigger on push to `main` and on pull requests. Matrix `os: [ubuntu-latest, macos-latest, windows-latest]`. Steps: checkout; `actions/setup-dotnet` using `global.json`; `dotnet restore --locked-mode` only after lock files exist (skip until then and record in PROGRESS.md); `STD-BUILD`; `STD-FORMAT`; install FFmpeg on the runner (ubuntu `sudo apt-get install -y ffmpeg`; macOS `brew install ffmpeg`; Windows `choco install ffmpeg -y`; runner provisioning is the only place `sudo` may appear, and the guard in T7.6 does not cover `.github/`); `STD-TEST`; `dotnet publish src/Drawplate.Console -c Release -r <rid> -o out` with rid `linux-x64`, `osx-arm64`, `win-x64` per matrix; run `out/drawplate --version`; print the size of the binary into the job summary. Pin every action by full commit SHA: look the SHAs up from each action's repository when you write the file; never copy them from memory; add a comment with the tag next to each SHA.
Verify: push a branch, `gh run watch` -> three green jobs. Record the run URL in PROGRESS.md. Commit: `ci: build, test and publish on three operating systems`.

**T1.5 Agent documents.** Depends: T1.4. Files: `AGENTS.md`, `PROGRESS.md`, `README.md`.
Do: AGENTS.md gains the section C conventions (copy C.1 to C.11) and the standard verification commands. PROGRESS.md gets the first entry (template I.6).
Verify: `grep -c "STD-BUILD" AGENTS.md` -> at least 1. Commit: `docs: add conventions to AGENTS.md`.

**T1.6 Spike S5: UI library cost.** Depends: T1.4. Files: `docs/decisions/0003-ui-library.md` only (use a throwaway project outside the repo for the measurement).
Do: publish a trivial AOT console app twice (plain `Console.WriteLine`; with Spectre.Console and a table, a progress bar and a prompt) for each CI OS; record binary size, cold start of `--version` (median of 20 runs), and any AOT or trim warning. Decision rule: adopt Spectre.Console if the size increase is under 6 MB and there are zero AOT warnings; otherwise record "plain renderer only" and P5 uses `PlainRenderer` with ANSI sequences. Set the size budget: measured full-app size plus 10 percent.
Verify: the ADR contains a table of measured numbers [V]. Commit: `docs: record UI library decision`.

### P2. Core domain (project Models; no IO)

**T2.1 QualityScale (F07).** Depends: T1.5. Files: `src/Drawplate.Models/Encoding/QualityScale.cs`, `tests/Drawplate.Tests/Unit/QualityScaleTests.cs`.
Do: `public static class QualityScale { public const int Default = 50; public static bool IsValid(int q); public static int ToCrf(int q); }` with `ToCrf = 51 - q / 2`; out-of-range throws `ArgumentOutOfRangeException`. Add `TryParse(string? text, out int q)`: digits only, invariant culture, 1 to 100.
Tests: `ToCrf_KnownPoints` (theory: 100 to 1, 99 to 2, 75 to 14, 50 to 26, 25 to 39, 2 to 50, 1 to 51); `ToCrf_OutOfRange_Throws` (0, 101, -1); `TryParse_Rejects` ("", "0", "101", "5x", " 5", "+5", "5.0", "٥"); `TryParse_Accepts` ("1", "50", "100").
Verify: `TEST FullyQualifiedName~QualityScaleTests` -> all passed. Commit: `feat(models): add QualityScale (F07)`.

**T2.2 SizeCaps (F09, F10).** Depends: T2.1. Files: `src/Drawplate.Models/Encoding/SizeCap.cs`, `.../SizeCaps.cs`, `tests/Drawplate.Tests/Unit/SizeCapsTests.cs`.
Do: `enum SizeCap { None, Sd, Hd, FullHd, Uhd4K }`. `SizeCaps.TryParse(string? text, out SizeCap cap)` accepts `sd hd fullhd 4k` case-insensitively and `none`/empty as `None`. `Box` returns 854x480, 1280x720, 1920x1080, 3840x2160. `VideoFilter(cap)` returns exactly the strings in I.1 (the even-scale string for `None`).
Tests: `VideoFilter_None_IsEvenScaleOnly`; `VideoFilter_Hd_EqualsGolden`; `VideoFilter_Sd_UsesBox854x480`; `TryParse_CaseInsensitive` ("HD", "FullHD", "4K"); `TryParse_Rejects` ("8k", "720").
Verify: `TEST FullyQualifiedName~SizeCapsTests`. Commit: `feat(models): add SizeCaps (F09, F10)`.

**T2.3 Encoders and profiles (F07, F08, F11, D.2).** Depends: T2.2. Files: `src/Drawplate.Models/Encoding/EncoderKind.cs`, `OperatingSystemKind.cs`, `EncoderSets.cs`, `EncoderProfiles.cs`, `tests/Drawplate.Tests/Unit/EncoderTests.cs`.
Do: enums per I.5. `EncoderSets.For(os)` returns the priority list including `Cpu` last (D.2). `EncoderSets.Id(kind)` returns `cpu nvenc qsv vaapi amf videotoolbox`; `Badge(kind)` returns `[CPU] [NVENC] [QSV] [VAAPI] [AMF] [VT]`. `EncoderSets.Parse(string text, OperatingSystemKind os)` returns `Ok(kind)`, `UnknownEverywhere` or `WrongOs`. `EncoderProfiles.Args(kind, quality)` returns the segments of I.1 for Cpu, Nvenc, Qsv and VideoToolbox; Vaapi and Amf throw `NotSupportedException("capture the golden vector first (T6.7)")`.
Tests: `Sets_PerOs` (three theory rows from D.2); `Parse_WrongOs_Amf_OnLinux`; `Parse_UnknownEverywhere_Foo`; `Args_Cpu_Q50`; `Args_Nvenc_Q80_UsesCrf11`; `Args_VideoToolbox_UsesQualityDirectly`.
Verify: `TEST FullyQualifiedName~EncoderTests`. Commit: `feat(models): add encoder sets and profiles`.

**T2.4 Argument vectors and golden tests (F14).** Depends: T2.3. Files: `src/Drawplate.Models/Encoding/FfmpegArgv.cs`, `tests/Drawplate.Tests/Golden/*.txt`, `tests/Drawplate.Tests/Unit/GoldenArgvTests.cs`.
Do: `FfmpegArgv.Convert(source, partial, kind, quality, cap)`, `FfmpegArgv.Probe(kind)`, `FfmpegArgv.Banner(source)` (= `-nostdin -hide_banner -i <source>`). Write the golden files from I.1: `cpu-q50.txt`, `cpu-q80-hd.txt`, `nvenc-q50.txt`, `qsv-q50.txt`, `probe-cpu.txt`, `probe-nvenc.txt`, `probe-qsv.txt`; format: one argv element per line, `SRC` and `PARTIAL` placeholders.
Tests: `Convert_Cpu_Q50_MatchesGolden`, `Convert_Cpu_Q80_Hd_MatchesGolden`, `Convert_Nvenc_Q50_MatchesGolden`, `Convert_Qsv_Q50_MatchesGolden`, `Probe_*_MatchesGolden`, `Convert_PathWithSpacesAndQuotes_IsSingleElement`.
Verify: `TEST FullyQualifiedName~GoldenArgvTests`. Commit: `feat(models): add FfmpegArgv with golden vectors (F14)`.

**T2.5 ProgressParser (F15).** Depends: T1.5. Files: `src/Drawplate.Models/Progress/ProgressSample.cs`, `ProgressParser.cs`, `tests/Drawplate.Tests/Unit/ProgressParserTests.cs`.
Do: `ProgressParser.Feed(string line)` returns `ProgressSample?` only on a `progress=` line. Keys: `out_time_us` (microseconds), `out_time_ms` (microseconds, used only when `out_time_us` absent in the block), `speed` (`1.5x` or `N/A`). Values `N/A` ignored. State resets after each block. `ProgressSample(TimeSpan Elapsed, double? Speed, bool Ended)`; `Fraction(duration)` helper returns null when duration is null or zero and clamps to 0..1.
Tests: `Block_WithUs_ReturnsElapsed`; `Block_WithOnlyMs_UsesMsAsMicroseconds`; `Block_NaValues_ReturnsZeroElapsedNoSpeed`; `End_SetsEnded`; `TwoBlocks_AreIndependent`; `Fraction_NullDuration_IsNull`; `Fraction_Clamps`.
Verify: `TEST FullyQualifiedName~ProgressParserTests`. Commit: `feat(models): add ProgressParser (F15)`.

**T2.6 InputRules (F01, F25, F26).** Depends: T1.5. Files: `src/Drawplate.Models/Planning/InputRules.cs`, `tests/Drawplate.Tests/Unit/InputRulesTests.cs`.
Do: `IsSupported(string fileName)` (extensions `mov mp4 mkv avi webm`, case-insensitive, name must have a stem), `IsHidden(string fileName)` (starts with `.`), `NameOrder` (`OrdinalIgnoreCase`, tie-break `Ordinal`).
Tests: `IsSupported_Theory` (`a.MOV` true, `a.Mp4` true, `a.txt` false, `.mp4` false, `a` false); `IsHidden_Theory`; `Order_CaseInsensitiveThenOrdinal` (`B.mov`, `a.mp4`, `A.mp4` gives `A.mp4`, `a.mp4`, `B.mov`).
Verify: `TEST FullyQualifiedName~InputRulesTests`. Commit: `feat(models): add InputRules (F01, F25, F26)`.

**T2.7 OutputNaming (F04).** Depends: T2.6. Files: `src/Drawplate.Models/Planning/OutputNaming.cs`, `tests/Drawplate.Tests/Unit/OutputNamingTests.cs`.
Do: `TargetName("My Clip.MOV")` returns `My Clip.mp4`; `PartialName("My Clip.MOV", 4242)` returns `.My Clip.4242.partial.mp4`; stem is the name without the last extension.
Tests: `TargetName_Theory` (`a.mov`, `a.b.mkv` to `a.b.mp4`, `x.MP4` to `x.mp4`); `PartialName_ContainsPidAndStartsWithDot`.
Verify: `TEST FullyQualifiedName~OutputNamingTests`. Commit: `feat(models): add OutputNaming (F04)`.

**T2.8 ConversionPlanner (F02, F05, F06).** Depends: T2.7. Files: `src/Drawplate.Models/Planning/IPlanFileSystem.cs`, `PlannedFile.cs`, `PlanStatus.cs`, `NameCollision.cs`, `ConversionPlan.cs`, `ConversionPlanner.cs`, `tests/Drawplate.Tests/Unit/PlannerTests.cs`, `tests/Drawplate.Tests/Unit/FakePlanFileSystem.cs`.
Do: `IPlanFileSystem { bool FileExists(string); bool DirectoryExists(string); string FullPath(string); string ResolveLinks(string); }`. `ConversionPlanner.Build(ImmutableArray<string> sources, string outputFolder, IPlanFileSystem fs, OperatingSystemKind os)` returns `ConversionPlan`. Per source, in order: not a file and not a directory gives `Missing` (W-01); directory gives `NotAFile` (W-02); unsupported extension gives `SkipUnsupported` (N-01); source (links resolved) inside the output folder gives `SkipInsideOutput`; target exists gives `SkipExists`; else `Convert`. Path comparison uses `OrdinalIgnoreCase` on Windows and macOS, `Ordinal` on Linux. Collisions: group `Convert` files by `TargetName` with `OrdinalIgnoreCase`; any group of two or more fills `Collisions`.
Tests: `Missing_File`, `Directory_Argument`, `Unsupported_Extension`, `InsideOutput_Skipped`, `ExistingTarget_Skipped`, `Collision_Detected_ClipMp4_ClipMov`, `Collision_CaseInsensitive`, `NoCollision_DifferentStems`, `Order_Preserved`.
Verify: `TEST FullyQualifiedName~PlannerTests`. Commit: `feat(models): add ConversionPlanner (F02, F05, F06)`.

**T2.9 FailureClassifier.** Depends: T1.5. Files: `src/Drawplate.Models/Failures/FailureKind.cs`, `FailureClassifier.cs`, `tests/Drawplate.Tests/Unit/FailureClassifierTests.cs`, `tests/Drawplate.Tests/Fixtures/stderr/*.txt`.
Do: `FailureKind { InvalidInput, NoVideoStream, EncoderUnavailable, NvencDriverTooOld, DeviceAccess, Unknown }`. `Classify(string stderrTail)` by case-insensitive substrings: `Invalid data found when processing input` gives InvalidInput; `Unknown encoder` or `Encoder not found` gives EncoderUnavailable; `Driver does not support the required nvenc API version` gives NvencDriverTooOld; `Permission denied` together with `/dev/dri` or `/dev/nvidia` gives DeviceAccess; otherwise Unknown. `ParseNvenc(string)` returns `(string? Required, string? Found, string? MinimumDriver)` from `Required: X Found: Y` and `The minimum required Nvidia driver for nvenc is N or newer`. `ShortReason(kind, tail)` returns the reason strings of section E (`invalid or corrupt input`, `no video stream`, `encoder failed: {last non-empty line}`).
Fixtures: capture real messages by running the commands (an invalid file: `ffmpeg -i bad.mp4 -f null -`; `-map 0:v:0` on an audio-only file); the NVENC text is taken from the public logs quoted in the research (mark that fixture `[I]` in a header comment).
Tests: `Classify_InvalidData`, `Classify_NvencTooOld_ParsesVersions`, `Classify_UnknownEncoder`, `Classify_Other_IsUnknown`, `ShortReason_UsesLastLine`.
Verify: `TEST FullyQualifiedName~FailureClassifierTests`. Commit: `feat(models): add FailureClassifier`.

**T2.10 Options and validation (F11, D.1, E-USAGE).** Depends: T2.3, T2.2, T2.1. Files: `src/Drawplate.Models/Cli/RawOptions.cs`, `ConvertOptions.cs`, `Message.cs`, `Messages.cs`, `OptionValidator.cs`, `tests/Drawplate.Tests/Unit/OptionValidatorTests.cs`.
Do: `RawOptions` holds strings and flags as parsed (no validation). `OptionValidator.Validate(RawOptions, OperatingSystemKind)` returns either `ConvertOptions` or a `Message` (id, text, hint, exit code). Order: encoder (E-USAGE-04 or E-USAGE-12), quality (E-USAGE-05), max size (E-USAGE-06), then `-i` with files (E-USAGE-11), then `-m` without terminal is checked later by the host. `Messages` holds every text of section E as constants and format methods; text must match section E character for character.
Tests: one test per message id with the exact expected text; `Validate_Defaults_AreQuality50_AutoEncoder_NoCap`; `Validate_CpuFlag_ForcesCpu`.
Verify: `TEST FullyQualifiedName~OptionValidatorTests`. Commit: `feat(models): add option validation and message catalog`.

**T2.11 Sanitizer and formatting (F27, D.4).** Depends: T1.5. Files: `src/Drawplate.Models/Formatting/Sanitizer.cs`, `Glyphs.cs`, `HumanFormat.cs`, `tests/Drawplate.Tests/Unit/FormattingTests.cs`.
Do: `Sanitizer.ForDisplay(string)` removes C0/C1 control characters and ESC; `Glyphs.Utf8` and `Glyphs.Ascii` records per D.4; `HumanFormat.Bytes(long)` (1024-based, `B KB MB GB`, one decimal except bytes), `HumanFormat.Time(TimeSpan)` (`m:ss`, `h:mm:ss`), `HumanFormat.Clip(string, int)`.
Tests: `Bytes_Theory` (0 to `0 B`, 1023 to `1023 B`, 1536 to `1.5 KB`, 128210145 to `122.3 MB`), `Time_Theory` (65 s to `1:05`, 3600 s to `1:00:00`), `Sanitizer_RemovesEsc`, `Clip_AddsMarker`.
Verify: `TEST FullyQualifiedName~FormattingTests`. Commit: `feat(models): add sanitizer and formatting helpers`.

**T2.12 FfmpegBannerParser (D.5, F15).** Depends: T1.5. Files: `src/Drawplate.Models/Ffmpeg/FfmpegBannerParser.cs`, `FfmpegVersion.cs`, `tests/Drawplate.Tests/Unit/BannerParserTests.cs`, `tests/Drawplate.Tests/Fixtures/ffmpeg/*.txt` (real outputs captured with the FFmpeg on your machine).
Do: `ParseVersion(string versionOutput)` handles `ffmpeg version 6.1.1-3ubuntu5`, `ffmpeg version n9.0.2-39-g7adfbe40dc-20261031`, `ffmpeg version 9.0.2-full_build-www.gyan.dev`, and master builds `N-128212-g17a4a474f4` (IsMaster true); `ParseEncoders(string)` returns the set of encoder names from `-encoders` lines (regex `^\s*[VAS][A-Z.]{5}\s+(\S+)`); `ParseDuration(string stderr)` reads `Duration: HH:MM:SS.xx` (null on `N/A`); `HasVideoStream(string stderr)` is true when a line matches `Stream #\d+:\d+.*: Video:`.
Tests: one per input sample above plus `ParseDuration_NA_IsNull`, `HasVideoStream_AudioOnly_False`.
Verify: `TEST FullyQualifiedName~BannerParserTests`. Commit: `feat(models): add FFmpeg output parsers`.

**T2.13 InstallAdvisor (F20, D.5, E-FF).** Depends: T2.10. Files: `src/Drawplate.Models/Setup/PlatformInfo.cs`, `InstallSuggestion.cs`, `InstallAdvisor.cs`, `tests/Drawplate.Tests/Unit/InstallAdvisorTests.cs`.
Do: implement exactly the suggestion rules of D.5 as a pure function of `PlatformInfo` (OS, architecture, set of tool names found on PATH, Linux id and id-like, Fedora release when known). Output: ordered `InstallSuggestion(Title, DisplayLines, Executable, Arguments, RunnableByApp)`. The not-found text of E-FF-01 is produced by `InstallAdvisor.NotFoundText(platform, suggestions)`. Windows with winget: first suggestion has `Executable = "winget"` and `Arguments = ["install","--id","Gyan.FFmpeg","-e","--accept-package-agreements","--accept-source-agreements"]`, `RunnableByApp = true`. Linux suggestions are never runnable.
Tests: `Windows_WithWinget_FirstIsWingetRunnable`; `Windows_WithoutWinget_ShowsE_FF_05_Text`; `Windows_WithScoop_AddsScoopRunnable`; `Windows_WithChoco_AddsChocoNotRunnable`; `MacOs_WithBrew_Runnable`; `MacOs_WithoutBrew_ShowsE_FF_06`; `Linux_Apt_PrintsSudoAptInstall_NotRunnable`; `Linux_Fedora_PrintsRpmFusionTwoStep`; `NotFoundText_Windows_MatchesCatalogExactly`.
Verify: `TEST FullyQualifiedName~InstallAdvisorTests`. Commit: `feat(models): add InstallAdvisor (F20)`.

### P3. Process layer, FFmpeg discovery, platform detection (project Services)

**T3.1 IProcessLauncher.** Depends: P2 approved. Files: `src/Drawplate.Services/Processes/IProcessLauncher.cs`, `ProcessLauncher.cs`, `RingBuffer.cs`, `src/Drawplate.Services/Processes/ProcessOptions.cs`.
Do: implement the interfaces in I.5. `Start` uses `ProcessStartInfo` with `ArgumentList`, `UseShellExecute=false`, `RedirectStandardOutput=true` and `RedirectStandardError=true` (both `false` when `Options.InheritConsole` is true; that mode is used for winget and brew so they show their own progress); stdin is never redirected. The only options are `Timeout` and `InheritConsole`. With `InheritConsole=true` nothing is redirected (used for winget and brew). stdout lines are exposed through `IAsyncEnumerable<string>`; stderr goes to a 64 KiB `RingBuffer` readable through `StderrTail`. `WaitAsync(ct)`: on cancellation call `Kill(entireProcessTree: true)`, wait up to 5 s, then throw `OperationCanceledException`. On `Timeout` kill the tree and return exit code `-1` with `TimedOut = true`. `DisposeAsync` kills a still-running process.
No tests yet (T3.3). Verify: STD-BUILD. Commit: `feat(services): add process launcher`.

**T3.2 FakeExe.** Depends: T3.1. Files: `tests/FakeExe/Program.cs`.
Do: a console program (not AOT) configured by the environment variable `FAKE_EXE_SCRIPT` (path to a text file; one JSON object per line) and `FAKE_EXE_LOG` (file appended with one line per start: the argv elements separated by U+001F). Script actions, one JSON object per line, executed in order: `{"stdout":"text"}`, `{"stderr":"text"}`, `{"sleepMs":100}`, `{"hang":true}`, `{"createFile":"path"}` (use `"@last"` to create the file named by the last argv element), `{"exit":0}`. Any object may carry `"if":"<text>"`: it then applies only when the argv elements joined by spaces contain that text, so one script can answer `-version`, `-encoders` and a conversion call differently. Unknown action: exit 99.
Verify: STD-BUILD. Commit: `test: add FakeExe`.

**T3.3 Process tests (tier T1).** Depends: T3.2. Files: `tests/Drawplate.Tests/Process/LauncherTests.cs`, `tests/Drawplate.Tests/Process/FakeExeHarness.cs`.
Tests: `Stdout_IsStreamedLineByLine`; `Stderr_TailIsBounded_At64KiB` (write 1 MiB to stderr, no deadlock, tail length at most 65536); `Timeout_KillsAndReportsTimedOut` (hang, 500 ms timeout, returns within 3 s); `Cancel_KillsProcessTree_Within3Seconds` (FakeExe starts a child FakeExe that hangs; after cancel both are gone; verify by pid); `ExitCode_IsReturned` (0 and 7); `ArgumentsWithSpacesAndQuotes_ArriveIntact` (log shows the exact elements).
Verify: `TEST Tier=T1` -> all passed on the current OS. Commit: `test: cover process launcher`.

**T3.4 FfmpegLocator (D.5).** Depends: T3.3, T2.12. Files: `src/Drawplate.Services/Ffmpeg/FfmpegLocator.cs`, `FfmpegCandidate.cs`, `tests/Drawplate.Tests/Process/LocatorTests.cs`.
Do: order and folders exactly as D.5. Inputs injected: environment accessor, PATH split, file-exists function, home folders. Output `FfmpegCandidate(Path, Source)` where Source is `Argument`, `Environment`, `Path` or `KnownFolder`. Glob for `Gyan.FFmpeg_*\ffmpeg-*\bin` is done through an injected directory enumerator. `--ffmpeg-path` and `DRAWPLATE_FFMPEG` never fall through (E-FF-02 on failure).
Tests: `Argument_Wins_OverEnvironment_OverPath`; `Environment_Unusable_DoesNotFallThrough`; `Path_Found`; `KnownFolder_WinGetLinks_FoundWhenNotOnPath`; `NothingFound_ReturnsNull`.
Verify: `TEST FullyQualifiedName~LocatorTests`. Commit: `feat(services): add FfmpegLocator`.

**T3.5 FfmpegInfoService.** Depends: T3.4. Files: `src/Drawplate.Services/Ffmpeg/FfmpegInfoService.cs`, `FfmpegInfo.cs`, `tests/Drawplate.Tests/Process/InfoServiceTests.cs`.
Do: run `<ffmpeg> -hide_banner -version` and `-hide_banner -encoders` (10 s timeout each) through the launcher; parse with T2.12; return `FfmpegInfo(Path, Version, Encoders, HasLibx264)` or a failure (`NotRunnable(reason)`, `TooOld(version)`). Minimum version 6.0 (master builds pass).
Tests (FakeExe scripts replaying fixture text): `Version_6_1_1_Ok`, `Version_5_1_TooOld`, `NotRunnable_ExitCode127`, `Encoders_ContainLibx264`, `Timeout_IsNotRunnable`.
Verify: `TEST FullyQualifiedName~InfoServiceTests`. Commit: `feat(services): add FfmpegInfoService`.

**T3.6 EncoderProbe (F12).** Depends: T3.5, T2.4. Files: `src/Drawplate.Services/Ffmpeg/EncoderProbe.cs`, `tests/Drawplate.Tests/Process/EncoderProbeTests.cs`.
Do: for each encoder in `EncoderSets.For(os)` in order run `FfmpegArgv.Probe(kind)` with a 15 s timeout; `cpu` passes only if `HasLibx264` and its probe passes. Return `ProbeResult(Kind, Passed, FailureKind?, StderrTail)` for all encoders. Sequential.
Tests: `AllPass`, `NvencFails_QsvPasses`, `Hang_IsKilledAt15s` (use a configurable timeout in tests), `NoLibx264_CpuFailsWithoutRunning`, `NvencDriverTooOld_IsClassified`.
Verify: `TEST FullyQualifiedName~EncoderProbeTests`. Commit: `feat(services): add EncoderProbe (F12)`.

**T3.7 MediaProbe.** Depends: T3.5, T2.12. Files: `src/Drawplate.Services/Ffmpeg/MediaProbe.cs`, `tests/Drawplate.Tests/Process/MediaProbeTests.cs`.
Do: `Inspect(path)` returns `MediaInfo(TimeSpan? Duration, bool HasVideo)`. If `ffprobe` exists (same folder as FFmpeg, then PATH): `ffprobe -v error -show_entries format=duration -of csv=p=0 <file>` and `ffprobe -v error -select_streams v:0 -show_entries stream=codec_type -of csv=p=0 <file>`. Otherwise run `FfmpegArgv.Banner(path)` and parse stderr with T2.12 (exit code 1 is expected). Timeout 15 s.
Tests: `Ffprobe_Present_UsesIt`; `Ffprobe_Absent_UsesBanner`; `AudioOnly_HasVideoFalse`; `DurationNA_IsNull`.
Verify: `TEST FullyQualifiedName~MediaProbeTests`. Commit: `feat(services): add MediaProbe`.

**T3.8 Platform and GPU detection.** Depends: T3.1. Files: `src/Drawplate.Services/Platform/PlatformDetector.cs`, `GpuDetector.cs`, `tests/Drawplate.Tests/Process/PlatformTests.cs`, `tests/Drawplate.Tests/Fixtures/sys/**`.
Do: `PlatformDetector` fills `PlatformInfo` (OS and architecture from `RuntimeInformation`; tool names found on PATH among `winget scoop choco brew apt-get dnf pacman zypper`; `/etc/os-release` `ID`, `ID_LIKE`, Fedora `VERSION_ID`). `GpuDetector` implements D.7 using an injected root path for `/sys` and an injected registry reader for Windows (real implementation uses `Microsoft.Win32.Registry`; AOT-safe). Fixture trees for Intel, AMD, NVIDIA and none.
Tests: `Linux_IntelFixture_HasGpu`, `Linux_NoDrm_NoGpu`, `Linux_OsRelease_Parsed`, `Windows_NvidiaMatchingId_HasGpu`, `Windows_BasicDisplayOnly_NoGpu`.
Verify: `TEST FullyQualifiedName~PlatformTests`. Commit: `feat(services): add platform and GPU detection`.

**T3.9 Spikes S1, S2, S8 (ADRs).** Depends: T3.7. Files: `docs/decisions/0004-process-spike.md`, `0005-ffmpeg-versions.md`, `0006-powershell-spike.md`.
Do: S1: publish the AOT binary and a throwaway console that streams stdout from real ffmpeg, kills the tree after 2 s, and checks that ffmpeg is gone within 3 s and the partial removed (Linux, macOS, Windows). S2: on the three CI runners record `ffmpeg -version`, whether `libx264` is present, and whether banner parsing equals ffprobe on three sample files; decide the minimum FFmpeg version (default 6.0). S8: on a Windows runner under `powershell` and `pwsh`: HttpClient streaming download with progress, SHA-256, extraction and starting a native process whose path contains spaces. Each ADR holds the commands run and their output [V].
Verify: three ADR files exist and contain [V] results. Commit: `docs: record spikes S1, S2 and S8`.

### P4. Conversion pipeline and command line = milestone M1

M1 scope: folder and file modes; CPU (libx264) and whatever hardware chain T6 later adds is **not** part of M1 (the chain is `[cpu]`); options `-i -o -q -s -c -t -l -h -V --ffmpeg-path --verbose --`; skip rules; collision refusal; unique partial plus atomic move; timestamps; progress parsing; plain output; exit codes 0, 1, 2, 3, 130; cancellation; AOT publish; smoke tests. Options `-m`, `-y`, `-n`, `setup`, `doctor` parse correctly but answer with the temporary message `E-TEMP-01: This option is not available yet.` (exit 2) until P7 and P9 replace it.

**T4.1 InputDiscovery and the real file system.** Depends: P3 approved. Files: `src/Drawplate.Services/Conversion/InputDiscovery.cs`, `RealPlanFileSystem.cs`, `tests/Drawplate.Tests/Integration/InputDiscoveryTests.cs`, `tests/Drawplate.Tests/Integration/UnixFactAttribute.cs`.
Do: `RealPlanFileSystem : IPlanFileSystem` using `File.Exists`, `Directory.Exists`, `Path.GetFullPath`, and `ResolveLinks` via `FileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)` (return the full path itself when there is no link). `InputDiscovery.FromFolder(folder)` enumerates the folder (top level only), keeps names passing `InputRules.IsSupported` and not `IsHidden`, sorts with `InputRules.NameOrder`; a missing folder returns E-USAGE-07.
Tests: `Folder_ListsSupportedOnly_InOrder`, `Folder_IgnoresSubfoldersAndDotFiles`, `MissingFolder_ReturnsE_USAGE_07`, `Symlinked_Source_ResolvedBeforeInsideOutputCheck` (`UnixFact`: skipped on Windows because creating symlinks needs privileges).
Verify: `TEST FullyQualifiedName~InputDiscoveryTests`. Commit: `feat(services): add InputDiscovery (F01, F26)`.

**T4.2 ConversionRunner.** Depends: T4.1. Files: `src/Drawplate.Services/Conversion/ConversionRunner.cs`, `FileResult.cs`, `RunnerEvents.cs`, `TimestampCopier.cs`, `tests/Drawplate.Tests/Process/RunnerTests.cs`, `tests/Drawplate.Tests/Integration/RunnerIntegrationTests.cs`.
Do (exact algorithm; one file at a time, sequential):
1. Status other than `Convert`: emit `Skipped` with the reason of section D.4 and continue.
2. `MediaProbe.Inspect`; `HasVideo == false`: `Failed("no video stream")`; do not start ffmpeg.
3. `partial = Path.Combine(outputFolder, OutputNaming.PartialName(name, Environment.ProcessId))`; delete a stale file of that exact name.
4. For each encoder in the chain: start ffmpeg with `FfmpegArgv.Convert(source, partial, kind, quality, cap)`; feed each stdout line to `ProgressParser`; report `FileProgress(fraction, speed, elapsed)`; await exit. Success = exit code 0 and `partial` exists with length above zero. Otherwise classify the stderr tail, delete the partial, remember the failure, try the next encoder.
5. On success: `File.Move(partial, target, overwrite: false)`. If that throws `IOException` because the target now exists: delete the partial and report `Skipped("output already exists")`. Copy timestamps with `TimestampCopier` unless `-t`. Record input size, output size, encoder, elapsed.
6. Every encoder failed: `Failed(FailureClassifier.ShortReason(...))`.
7. Always, in `finally`: delete the partial if it still exists. A cancellation token cancels the launcher (process tree killed), then `OperationCanceledException` propagates after cleanup.
`TimestampCopier`: set last-write and last-access times from the source; on Windows also creation time.
Tests (T1, FakeExe as ffmpeg): `Fallback_FirstEncoderFails_SecondUsed`, `NoVideoStream_FailsWithoutStartingFfmpeg` (FakeExe log is empty), `Cancel_RemovesPartial`, `TargetAppearsMidRun_IsSkipped`, `AllEncodersFail_ReportsReason`.
Tests (T2, real ffmpeg, fixtures I.2): `Converts_Landscape_H264_Yuv420p_Aac_EvenSize`, `Timestamps_Copied`, `Timestamps_NotCopied_WhenDisabled`, `Rerun_SkipsExisting`.
Verify: `TEST FullyQualifiedName~Runner` -> all passed. Commit: `feat(services): add ConversionRunner (F04, F05, F13, F14, F17)`.

**T4.3 OptionTable and ArgParser (D.1).** Depends: T2.10. Files: `src/Drawplate.Console/Cli/OptionTable.cs`, `ParsedCommand.cs`, `ArgParser.cs`, `tests/Drawplate.Tests/Cli/ArgParserTests.cs`.
Do: one table lists every option of D.1 (short, long names and aliases, arity, setter into `RawOptions`); the parser and the help text both read it. Implement the parsing rules of D.1 exactly. Result: `ParsedCommand.Convert(RawOptions, files)`, `Setup(RawOptions)`, `Doctor`, `Help`, `Version` or `Error(Message)`.
Tests (theory rows): `-q50`; `-q 50`; `--quality=50`; `--quality 50`; `-tn` (two flags); `-oout`; `--input-dir x`; `--install` equals `-y`; `--list` equals `-l`; `-V --bogus` returns Version (acts immediately); `--bogus` gives E-USAGE-01 long form; `-z` gives E-USAGE-01 short form; `-q` alone gives E-USAGE-02; `--quality` alone gives E-USAGE-02; `--cpu=1` gives E-USAGE-03; `setup -y` is Setup; `./setup` is a file; `-- setup` is a file; `--` keeps later `-x` as a file name.
Verify: `TEST FullyQualifiedName~ArgParserTests`. Commit: `feat(console): add option table and parser (D.1)`.

**T4.4 PlainRenderer (D.4).** Depends: T2.11. Files: `src/Drawplate.Console/Ui/IRenderer.cs`, `PlainRenderer.cs`, `tests/Drawplate.Tests/Cli/PlainRendererTests.cs`.
Do: `IRenderer { PlanCard(...); FileStarted(...); FileProgress(...); FileFinished(...); Summary(...); Note(string); }`. `PlainRenderer` prints the plan card, per-file lines and summary exactly as D.4 with no colors and no live lines; glyph set chosen by `Glyphs`. `FileProgress` does nothing in `PlainRenderer`.
Tests (exact string comparison, both glyph sets): `PlanCard_Utf8`, `PlanCard_Ascii`, `OkLine`, `FailLine`, `SkipLine`, `Summary_WithFailures`, `Summary_AllOk`, `Names_AreSanitized`.
Verify: `TEST FullyQualifiedName~PlainRendererTests`. Commit: `feat(console): add PlainRenderer (D.4)`.

**T4.5 SignalHandler (F23).** Depends: T1.2. Files: `src/Drawplate.Console/Signals/SignalHandler.cs`, `tests/Drawplate.Tests/Cli/SignalTests.cs`, `tests/FakeExe/Program.cs` (only if the `if` property is missing).
Do: register `PosixSignalRegistration` for `SIGINT`, `SIGTERM`, `SIGHUP`, `SIGQUIT` (skip registrations the platform rejects) and `Console.CancelKeyPress`; every trigger calls `CancellationTokenSource.Cancel()` once and sets `e.Cancel = true` so the host returns exit 130 itself. Implements `IDisposable`.
Tests (`UnixFact`): start the built `drawplate` with `--ffmpeg-path <FakeExe>` whose script hangs on the conversion call; send SIGTERM to the Drawplate PID only: `Sigterm_ToPid_ExitsWithin5Seconds_Code130`, `Sigterm_RemovesPartial_NoFakeExeLeft`, `Sigint_Exits130`.
Verify: `TEST FullyQualifiedName~SignalTests` (Linux and macOS). Commit: `feat(console): add signal handling (F23)`.

**T4.6 AppHost and the convert flow.** Depends: T4.2, T4.3, T4.4, T4.5, T3.6, T3.8. Files: `src/Drawplate.Console/AppHost.cs`, `Program.cs`, `tests/Drawplate.Tests/Cli/CliTests.cs`.
Do the flow in this exact order and stop at the first failing step with the stated message and exit code:
1. Parse (T4.3). Help: print help text generated from the option table, exit 0. Version: N-VERSION, exit 0. Parse error: print it, exit 2. Options `-m -y -n` and subcommands `setup`, `doctor`: print E-TEMP-01, exit 2.
2. Validate (`OptionValidator`), exit 2 on error.
3. Detect the platform. Locate FFmpeg (`FfmpegLocator`); not found: print `InstallAdvisor.NotFoundText` (E-FF-01), exit 3. `FfmpegInfoService`: not runnable E-FF-02, too old E-FF-07, exit 3.
4. Probe encoders (`EncoderProbe`). `-l`: print working encoder ids (F18), exit 0. Build the chain (for M1: `[cpu]` when `libx264` works). Empty chain: E-FF-03, exit 3.
5. Resolve input and output folders; `-o` path is a file: E-USAGE-08, exit 2. Discover inputs and build the plan (`ConversionPlanner`). Print W-01, W-02, N-01 lines. Collisions: E-USAGE-09, exit 2. Nothing to convert and nothing skipped: E-RUN-01, exit 1.
6. Create the output folder. Print the plan card. Run `ConversionRunner` with the cancellation token from `SignalHandler`. Print per-file lines.
7. Print the summary. Exit 0 when no file failed, else 1. On cancellation print N-INT and exit 130.
`--verbose` prints each ffmpeg argument list and each stderr tail to stderr. `catch (Exception)` only here: print `Unexpected error: {message}` to stderr, exit 1.
Tests (T2, real CLI process, real ffmpeg): `Cli_FixtureFolder_FiveOutputs_OneFailure_Exit1`, `Cli_Rerun_ZeroConversions_Exit1_BecauseBadMp4FailsAgain`, `Cli_Collision_Exit2_NoFfmpegStarted`, `Cli_OutputIsFile_Exit2`, `Cli_NoFiles_Exit1`, `Cli_UnknownOption_Exit2`, `Cli_ListEncoders_PrintsCpu`, `Cli_Piped_ContainsNoEscByte`, `Cli_FfmpegMissing_Exit3_PrintsCatalogText` (PATH emptied), `Cli_FilesMode_DefaultOutput_IsCompressedInCwd`.
Verify: `TEST FullyQualifiedName~CliTests`. Commit: `feat(console): wire the convert flow (F01 to F22)`.

**T4.7 Integration fixtures and regression tests (I.2, I.3).** Depends: T4.6. Files: `tests/Drawplate.Tests/Integration/MediaFixtures.cs`, `FfmpegFactAttribute.cs`, `RegressionTests.cs`.
Do: `MediaFixtures` generates the files of I.2 into a temp folder with the real `ffmpeg` (one folder per test class, cached). Implement one test per row of I.3, named as the row. `FfmpegFact` fails when FFmpeg is missing and `CI` is set.
Verify: `TEST Tier=T2` -> every row of I.3 passed. Commit: `test: add fixtures and vneat regression tests`.

**T4.8 CI hardening and AOT smoke.** Depends: T4.7. Files: `.github/workflows/ci.yml`.
Do: add (a) publish with `-p:Version=0.0.0-ci` and assert `out/drawplate --version` prints `drawplate 0.0.0-ci`; (b) smoke: generate a 1-second clip with ffmpeg, run `out/drawplate -o out/c clip.mp4`, assert exit 0 and the output exists; (c) Unix only: start a conversion of a 60-second generated clip, `kill -TERM` the Drawplate PID after 2 s, assert exit code 130 within 5 s and no `.partial` file; (d) compare the binary size with the budget in ADR 0003 and fail when it is exceeded; (e) T2 runs on all three OS.
Verify: all jobs green on a branch; run URL in PROGRESS.md. Commit: `ci: add AOT smoke, cancellation smoke and size budget`.

**T4.9 README quick start and baseline refresh.** Depends: T4.8. Files: `README.md`, `docs/VNEAT-BASELINE.md`.
Do: README sections "What it is", "Install" (placeholder until P8), "Quick start", "Options" (generated from the option table: add a test `Readme_OptionsTable_MatchesOptionTable`), "Exit codes". Re-diff the three vneat scripts against the baseline (`git log -1 -- Bash/vneat Zsh/vneat.zsh PowerShell/vneat.ps1` in WorkWithIO); if they changed, list the changes in PROGRESS.md as [U] for the owner.
Verify: `TEST FullyQualifiedName~Readme`. Commit: `docs: add quick start and refresh baseline`.

**M1 completion criteria (all must hold; report each with its evidence):**
1. STD-BUILD, STD-FORMAT and STD-TEST pass locally and in CI on Linux, macOS and Windows.
2. Architecture tests (T1.3) pass.
3. The fixture-folder CLI test shows five outputs, h264 + yuv420p + AAC, even dimensions, mtime equal to the source; `bad.mp4` fails; exit 1; under 60 s on CI.
4. Collision test: exit 2 and zero ffmpeg starts.
5. SIGTERM smoke on Linux and macOS: exit 130 within 5 s, no partial file. On Windows the cancellation test passes or the gap is in PROGRESS.md as [U] with an issue.
6. AOT publish works for linux-x64, osx-arm64 and win-x64 with zero warnings; `--version` equals the build version; binary sizes recorded.
7. README and baseline committed; PROGRESS.md lists verified and unverified items; CI run URL recorded.

### P5. Terminal UI (Console project only)

**T5.1 Theme detection.** Depends: M1 approved, T1.6 decision. Files: `src/Drawplate.Console/Ui/Theme.cs`, `tests/Drawplate.Tests/Cli/ThemeTests.cs`.
Do: `Theme.Detect(IEnvironment)` decides colors (stdout is a terminal and `NO_COLOR` unset), glyph set (UTF-8 when `Console.OutputEncoding.CodePage == 65001` and `TERM != dumb`), width (console width, 80 when unknown), `IsInteractive` (stdin and stdout terminals).
Tests: `NoColor_DisablesColors`, `Piped_IsPlain`, `Dumb_UsesAscii`, `Width_FallsBackTo80`.
Verify: `TEST FullyQualifiedName~ThemeTests`. Commit: `feat(console): add theme detection`.

**T5.2 Cards with Spectre.Console (only if ADR 0003 adopted it).** Depends: T5.1. Files: `Drawplate.Console.csproj` (add the package), `Directory.Packages.props`, `src/Drawplate.Console/Ui/SpectreRenderer.cs`, `tests/Drawplate.Tests/Cli/SpectreRendererTests.cs`.
Do: `SpectreRenderer : IRenderer` renders the plan card and summary card of D.4 as Spectre panels/tables with the same text content; colors from the theme; uses `Spectre.Console.Testing.TestConsole` in tests. Verify the package version exists before pinning.
Tests: `PlanCard_ContainsAllRows`, `Summary_ContainsAllRows`, `NoColor_HasNoEscBytes`.
Verify: `TEST FullyQualifiedName~SpectreRendererTests` and the AOT publish has zero warnings. Commit: `feat(console): add Spectre cards`.

**T5.3 Live progress.** Depends: T5.2. Files: `src/Drawplate.Console/Ui/LiveProgress.cs`, tests.
Do: two live lines per D.4, redrawn at most 10 times per second, only when `Theme.IsInteractive`; stopped and cleared before a per-file result line is printed; never used when output is piped.
Tests: `Live_NotStarted_WhenPiped`; `Live_Throttled_To10Hz` (fake clock); `Live_UnknownFraction_ShowsDashes`.
Verify: `TEST FullyQualifiedName~LiveProgressTests`. Commit: `feat(console): add live progress`.

**T5.4 Renderer selection and per-file lines.** Depends: T5.3. Files: `src/Drawplate.Console/AppHost.cs`, tests.
Do: AppHost uses `SpectreRenderer` on a terminal and `PlainRenderer` otherwise; the text of every line is identical to `PlainRenderer` apart from color.
Tests: `Cli_Piped_UsesPlainRenderer_NoEsc`, `Cli_NoColorEnv_NoEsc`.
Verify: `TEST FullyQualifiedName~CliTests`. Commit: `feat(console): choose renderer by terminal`.

### P6. Hardware encoders and the hardware note

**T6.1 Capture golden vectors (manual; owner or an agent with the hardware).** Depends: M1 approved. Files: `tests/Drawplate.Tests/Golden/vaapi-q50.txt`, `amf-q50.txt`, `videotoolbox-q50.txt`, `docs/HARDWARE.md`.
Do: build a shim (a copy of FakeExe configured to log its argv, print nothing and create the output file named by the last argument) and put it first on PATH as `ffmpeg` and `ffprobe`; run the real scripts for each encoder: `Bash/vneat -e vaapi -q 50 clip.mov` on a Linux machine with `/dev/dri/renderD*` (or an equivalent that makes the script accept VAAPI), `PowerShell/vneat.ps1 -Encoder amf -Quality 50 clip.mov` on Windows, `Zsh/vneat.zsh -e videotoolbox -q 50 clip.mov` on macOS. Save the logged argument lists in the golden file format. Record each result in `docs/HARDWARE.md` [V].
Verify: three golden files exist. Commit: `test: add captured hardware vectors`.

**T6.2 Encoder chain builder (F11, F13).** Depends: T2.3. Files: `src/Drawplate.Models/Encoding/EncoderChain.cs`, `tests/Drawplate.Tests/Unit/EncoderChainTests.cs`.
Do: `Build(requested, os, probeResults)`: `auto` gives working encoders in priority order with `cpu` last when it works; a specific encoder gives `[that]` (when it works) plus `cpu` (when it works); a requested encoder that did not pass gives `Error(E-RUN-02)`. Add E-RUN-02 to `Messages`: `Encoder '{value}' is not working on this machine.` Hint: `Run 'drawplate --list-encoders' to see what works.` (stderr, exit 1).
Tests: `Auto_Linux_NvencQsvCpu`, `Auto_OnlyCpu`, `Specific_Qsv_ThenCpu`, `Specific_NotWorking_ReturnsE_RUN_02`, `CpuOnly_NoLibx264_Empty`.
Verify: `TEST FullyQualifiedName~EncoderChainTests`. Commit: `feat(models): add EncoderChain`.

**T6.3 VAAPI profile and device discovery.** Depends: T6.1, T6.2. Files: `src/Drawplate.Models/Encoding/EncoderProfiles.cs` (extend), `FfmpegArgv.cs` (extend), `src/Drawplate.Services/Platform/VaapiDevice.cs`, tests.
Do: implement exactly the captured vector (device argument and the `format=nv12,hwupload` filter placement). `VaapiDevice.Find()` returns the first `/dev/dri/renderD*` (sorted) that can be opened for reading; null when none.
Tests: `Args_Vaapi_MatchesGolden`, `Device_FirstAccessible_Chosen` (fixture tree), `Device_None_ReturnsNull`.
Verify: `TEST FullyQualifiedName~Vaapi`. Commit: `feat: add VAAPI profile`.

**T6.4 AMF and VideoToolbox profiles.** Depends: T6.1. Files: `EncoderProfiles.cs`, tests.
Do: implement the captured vectors for AMF and VideoToolbox; remove the `NotSupportedException` branches.
Tests: `Args_Amf_MatchesGolden`, `Args_VideoToolbox_MatchesGolden`.
Verify: `TEST FullyQualifiedName~GoldenArgvTests`. Commit: `feat(models): add AMF and VideoToolbox profiles`.

**T6.5 Use the hardware chain in the convert flow (F12, F13).** Depends: T6.2, T6.3, T6.4, T4.6. Files: `src/Drawplate.Console/AppHost.cs`, `src/Drawplate.Services/Ffmpeg/EncoderProbe.cs` (VAAPI device), tests.
Do: replace the M1 chain with `EncoderChain.Build`; `--list-encoders` prints the chain order of working encoders; the plan card shows the chain; the summary counts encoders actually used; `-e X` rules of F11 and E-RUN-02.
Tests (T1, FakeExe): `Chain_FirstFails_FallsBackToCpu_PerFile`, `ListEncoders_PrintsWorkingInOrder`, `SpecificNotWorking_Exit1`.
Verify: `TEST FullyQualifiedName~Chain`. Commit: `feat(console): use hardware encoder chain`.

**T6.6 Hardware note (F30, D.7).** Depends: T6.5, T3.8. Files: `src/Drawplate.Models/Setup/HardwareNote.cs`, AppHost, tests.
Do: pure `HardwareNote.For(hasGpu, probeResults)` returns null, N-GPU-01 or N-GPU-02 (with the minimum driver when parsed). AppHost prints it once after the plan card; it is a plain note (no warning styling), written to stdout; with `-l` it goes to stderr.
Tests: `Note_GpuAndNoHwEncoder_Gpu01`, `Note_NvencTooOld_Gpu02WithMinimum`, `Note_NoGpu_Null`, `Note_HwEncoderWorks_Null`, `Cli_ExitCode_Unchanged_ByNote`.
Verify: `TEST FullyQualifiedName~HardwareNote`. Commit: `feat: add neutral hardware note (F30)`.

**T6.7 Hardware checklist (manual).** Depends: T6.5. Files: `docs/HARDWARE.md`.
Do: for each vendor available (NVIDIA, Intel, AMD, Apple): record the OS, GPU, driver version, FFmpeg version and source, `drawplate -l` output, and a conversion of the fixture folder with `-e <encoder>`; note failures with the stderr tail. Mark each result [V].
Verify: HARDWARE.md has one filled section per tested vendor. Commit: `docs: record hardware results`.

### P7. FFmpeg acquisition and doctor (winget first; no downloads by Drawplate)

Everything here follows D.5 to D.7. Drawplate never downloads FFmpeg itself and never runs an elevated command.

**T7.1 Prompt abstraction.** Depends: M1 approved. Files: `src/Drawplate.Services/Interaction/IPrompt.cs`, `ConsolePrompt.cs`, `tests/Drawplate.Tests/Cli/PromptTests.cs`.
Do: `IPrompt { bool IsInteractive { get; } bool Confirm(string question, bool defaultYes); string? Ask(string label, string? current); }`. `ConsolePrompt(TextReader input, TextWriter output, bool inputIsTerminal, bool outputIsTerminal)`. `Confirm` accepts `y`, `yes`, `n`, `no` (any case); empty returns the default; anything else re-asks, at most 3 times, then returns false. `IsInteractive` is true only when both streams are terminals.
Tests: `Confirm_Empty_UsesDefault`, `Confirm_Yes_No_Variants`, `Confirm_Invalid_ThreeTimes_ReturnsFalse`, `IsInteractive_FalseWhenRedirected`.
Verify: `TEST FullyQualifiedName~PromptTests`. Commit: `feat(services): add IPrompt`.

**T7.2 InstallRunner.** Depends: T7.1, T3.1. Files: `src/Drawplate.Services/Setup/InstallRunner.cs`, `tests/Drawplate.Tests/Process/InstallRunnerTests.cs`.
Do: `Run(InstallSuggestion s)` refuses a suggestion with `RunnableByApp == false` (throws `InvalidOperationException`); otherwise starts `s.Executable` with `s.Arguments` through the launcher with `InheritConsole = true` (the package manager shows its own progress) and returns `InstallRunResult(Started, ExitCode)`; an executable that cannot be started gives `Started = false`.
Tests (FakeExe named `winget`, first on PATH): `Winget_ArgumentsAreExact`, `ExitCode_IsReturned`, `NotRunnable_Throws`, `ExecutableMissing_NotStarted`.
Verify: `TEST FullyQualifiedName~InstallRunnerTests`. Commit: `feat(services): add InstallRunner`.

**T7.3 AcquisitionFlow (D.5).** Depends: T7.2, T3.4, T2.13. Files: `src/Drawplate.Services/Setup/AcquisitionFlow.cs`, `AcquisitionOutcome.cs`, `tests/Drawplate.Tests/Process/AcquisitionFlowTests.cs`.
Do: implement the four-step flow of D.5 exactly. Inputs: mode (`Convert` or `SetupCommand`), `AssumeYes`, `NeverInstall`, `PlatformInfo`, the locator, `IPrompt`, `InstallRunner`, a text sink. Output: `Found(candidate)`, `Exit(code)` after printing the catalog messages.
Tests (one per row): `NeverInstall_PrintsE_FF_01_Exit3_NoPrompt`; `AssumeYes_Winget_RunsWithoutPrompt_ThenFound`; `AssumeYes_Linux_NothingRunnable_Exit3`; `Interactive_Yes_InstallOk_Found`; `Interactive_Yes_InstallOk_StillNotFound_E_FF_04`; `Interactive_Yes_InstallFails_E_FF_08`; `Interactive_No_Exit3`; `NonInteractive_PrintsE_FF_01_Exit3`; `Windows_NoWinget_PrintsE_FF_05`; `SetupCommand_FfmpegPresent_PrintsN_SETUP_01_Exit0`.
Verify: `TEST FullyQualifiedName~AcquisitionFlowTests`. Commit: `feat(services): add FFmpeg acquisition flow (F20)`.

**T7.4 Wire the flow into the host.** Depends: T7.3, T4.6. Files: `src/Drawplate.Console/AppHost.cs`, `tests/Drawplate.Tests/Cli/SetupCliTests.cs`.
Do: remove E-TEMP-01 for `-y`, `-n` and `setup`. Convert flow step 3 calls `AcquisitionFlow` (mode Convert) when the locator returns nothing or the candidate is unusable. `-y` without `setup`: run the flow, then exit 0 after N-OK-01 or N-SETUP-01 (vneat's "install, then exit"). `-n` is passed as `NeverInstall`. `drawplate setup` runs mode SetupCommand.
Tests (real CLI process; PATH points to a folder holding FakeExe copies named `winget` or `brew`): `Cli_Y_RunsInstallThenExit0`, `Cli_N_NoPrompt_Exit3`, `Cli_Setup_FfmpegPresent_Exit0`, `Cli_Convert_Missing_NonInteractive_Exit3`.
Verify: `TEST FullyQualifiedName~SetupCliTests`. Commit: `feat(console): wire setup, -y and -n (F20)`.

**T7.5 DoctorService (F29, D.6).** Depends: T7.4, T6.6. Files: `src/Drawplate.Services/Setup/DoctorService.cs`, `src/Drawplate.Models/Setup/DoctorReport.cs`, `tests/Drawplate.Tests/Process/DoctorTests.cs`.
Do: produce the lines of D.6 in that order; statuses `ok missing broken outdated note`; hardware note via `HardwareNote`; exit 3 when FFmpeg is missing, broken or outdated, else 0. Wire `drawplate doctor`.
Tests: `AllOk_Exit0_ExactLines`, `Missing_ShowsE_FF_01_Exit3`, `Broken_ShowsE_FF_02_Exit3`, `Outdated_Exit3`, `NoFfprobe_StillOk`, `NoLibx264_NoHwEncoder_Broken_E_FF_03`, `Note_DoesNotChangeExit`.
Verify: `TEST FullyQualifiedName~DoctorTests`. Commit: `feat: add doctor command (F29)`.

**T7.6 No-elevation and single-launcher guards (A11).** Depends: T7.4. Files: `tests/Drawplate.Tests/Architecture/NoElevationTests.cs`.
Do: locate the repository root from the test assembly path. Tests: `NoElevationTokens_OutsideAllowlist` (case-insensitive tokens `sudo`, `pkexec`, `runas`, `gsudo` in `src/**/*.cs` and `scripts/src/**`; allowed only in `InstallAdvisor.cs`, `Messages.cs` and their tests, as display text); `NoShellExecute` (`UseShellExecute = true` and `.Verb` forbidden everywhere); `ProcessStartInfo_OnlyInLauncher` (only `ProcessLauncher.cs` may construct it).
Verify: `TEST FullyQualifiedName~NoElevationTests`. Commit: `test: guard against elevation and ad-hoc process starts`.

**T7.7 README troubleshooting and FFMPEG.md.** Depends: T7.5. Files: `README.md`, `docs/FFMPEG.md`, `tests/Drawplate.Tests/Cli/ReadmeTests.cs`.
Do: write the section "Troubleshooting FFmpeg" with every entry of H.2 and `docs/FFMPEG.md` per H.3. Test `Readme_Troubleshooting_HasAllEntries` checks that each heading of H.2 exists and that every `E-FF-*` message mentions a heading that exists.
Verify: `TEST FullyQualifiedName~ReadmeTests`. Commit: `docs: add FFmpeg troubleshooting`.

### P8. Installer scripts (section G is the specification)

**T8.1 Module layout and generator.** Depends: M1 approved. Files: `scripts/src/sh/*.sh`, `scripts/src/ps1/*.ps1` (empty modules named in G.6), `scripts/build-installers.ps1`, `scripts/dist/.gitkeep`.
Do: the generator concatenates the modules in the order listed in G.6 into `scripts/dist/install.sh` and `scripts/dist/install.ps1` and writes a header comment with the module names. It must run on Windows PowerShell 5.1 and PowerShell 7. CI regenerates and fails if `git diff --exit-code scripts/dist` is not clean (job added in T8.6).
Verify: `pwsh scripts/build-installers.ps1` and `powershell -File scripts/build-installers.ps1` produce byte-identical output (`sha256sum` equal). Commit: `build(scripts): add installer generator`.

**T8.2 install.sh.** Depends: T8.1, T4.8. Files: `scripts/src/sh/*.sh`, `scripts/tests/sh/*.sh`, `scripts/tests/serve_release.py`.
Do: implement G.1 to G.3 and G.4 for POSIX sh. Tests are shell scripts run by CI against a local `python3 -m http.server` bound to `127.0.0.1` that serves a fake release tree built from the real published binary; cases T-INS-01 to T-INS-09 (Unix rows).
Verify: `shellcheck scripts/dist/install.sh` clean; `sh scripts/tests/sh/run.sh` -> all cases passed. Commit: `feat(scripts): add install.sh`.

**T8.3 install.ps1.** Depends: T8.1, T4.8. Files: `scripts/src/ps1/*.ps1`.
Do: implement G.1 to G.5 for Windows PowerShell 5.1 and PowerShell 7+ in one file. No other task may touch these files.
Verify: PSScriptAnalyzer (T8.4) clean; the scan `Select-String -Path scripts/dist/install.ps1 -Pattern '[^\x00-\x7F]'` finds nothing. Commit: `feat(scripts): add install.ps1`.

**T8.4 Pester tests and analyzer settings.** Depends: T8.3. Files: `scripts/tests/ps1/install.Tests.ps1`, `scripts/PSScriptAnalyzerSettings.psd1`.
Do: Pester 5.x tests for every PowerShell row of G.4, run under both `powershell` and `pwsh`. Analyzer settings enable `PSUseCompatibleSyntax` (targets 5.1 and 7.0), `PSUseCompatibleCommands` (one Windows 5.1 profile and one Windows 7.x profile **taken from the profile files shipped with PSScriptAnalyzer; do not invent names**), `PSUseCompatibleTypes`, `PSAvoidUsingCmdletAliases`, `PSAvoidUsingPositionalParameters`, `PSUseDeclaredVarsMoreThanAssignments`.
Verify: `Invoke-ScriptAnalyzer -Path scripts/dist/install.ps1 -Settings scripts/PSScriptAnalyzerSettings.psd1` -> no findings; `Invoke-Pester scripts/tests/ps1` under both shells -> `Failed: 0`. Commit: `test(scripts): add Pester tests and analyzer rules`.

**T8.5 Progress and step display tests.** Depends: T8.2, T8.3. Files: `scripts/tests/sh/progress.sh`, `scripts/tests/ps1/progress.Tests.ps1`.
Do: assert the contract of G.2 on captured output: eight step lines `[n/8] ...` each ending `[ok]` or `[skipped]`; download progress lines in terminal-less mode at 25, 50, 75 and 100 percent; the bar-line renderer function given sample inputs returns the exact strings of G.2; no non-ASCII byte in either installer's output; colors absent when `NO_COLOR` is set.
Verify: both suites pass. Commit: `test(scripts): cover progress and step output`.

**T8.6 Installer CI.** Depends: T8.4, T8.5. Files: `.github/workflows/installers.yml`.
Do: jobs: `generated-up-to-date` (regenerate and `git diff --exit-code`); `sh` on `ubuntu-latest` and `macos-latest` (ShellCheck, tests, and an end-to-end run as a non-root user inside a container for Linux); `windows` with matrix `shell: [powershell, pwsh]` on `windows-2022` and `windows-latest` (analyzer, Pester). Trigger on pull requests that touch `scripts/**`. Pin actions by commit SHA.
Verify: all jobs green. Commit: `ci: add installer workflow`.

### P9. Interactive menu

**T9.1 Menu (F19).** Depends: M1 approved, T7.1. Files: `src/Drawplate.Services/Interaction/MenuService.cs`, `tests/Drawplate.Tests/Cli/MenuTests.cs`.
Do: exactly the prompts of F19: `Encoder` (numbered: `1 auto` then the working encoders; default 1), `Quality` (default current; invalid: E-USAGE-05 text but exit 2), `Max size` (default current or `none`). Invalid encoder number: E-MENU-01. Invalid size: E-MENU-02. Not interactive: E-USAGE-10 before any prompt.
Tests with a scripted `IPrompt`: `Defaults_AcceptedWithEmptyAnswers`, `ChoosesQsv`, `InvalidChoice_E_MENU_01`, `InvalidQuality`, `InvalidSize_E_MENU_02`, `NotInteractive_E_USAGE_10`.
Verify: `TEST FullyQualifiedName~MenuTests`. Commit: `feat(services): add interactive menu (F19)`.

**T9.2 Wire `-m`.** Depends: T9.1. Files: `AppHost.cs`, tests.
Do: remove E-TEMP-01 for `-m`; the menu runs after probing and before planning, and its answers override the parsed options.
Tests: `Cli_Menu_NonInteractive_Exit2`, `Cli_Menu_AnswersApplied` (pseudo-terminal helper on Unix; mark `UnixFact`).
Verify: `TEST FullyQualifiedName~MenuTests`. Commit: `feat(console): wire interactive mode`.

### P10. Release and documentation

**T10.1 Release workflow.** Depends: P8 approved. Files: `.github/workflows/release.yml`.
Do: adapt AriaSharp's `release.yml` (read it first): manual dispatch from `main` with a `version` input matching `^v\d+\.\d+\.\d+$`; matrix publish for `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64` with `-p:Version=<without v>`; archives `drawplate-<version>-<rid>.tar.gz` (Unix) and `.zip` (Windows) containing the binary, `LICENSE` and `THIRD-PARTY-NOTICES.md`; `SHA256SUMS` with lines `<hash>  <file>`; build-provenance attestation; `gh release create`. Smoke step per archive: extract, `drawplate --version` must print `drawplate <version>`.
Verify: a dry run on a test tag in a fork. Commit: `ci: add release workflow`.

**T10.2 Installer end-to-end against a real release.** Depends: T10.1. Files: `.github/workflows/installers.yml`.
Do: add a job that runs the real installers against the latest published release on all three OS and asserts `drawplate --version`, then `drawplate doctor` (exit 0 or 3, never a crash).
Verify: job green after the first release. Commit: `ci: test installers against a release`.

**T10.3 Notices and license.** Files: `THIRD-PARTY-NOTICES.md`, `LICENSE`.
Do: list every dependency with license (read from the restored packages, not from memory); state that FFmpeg is not distributed with Drawplate and is licensed by its own terms (GPL when built with libx264).
Verify: `grep -c "Spectre" THIRD-PARTY-NOTICES.md` is at least 1 when Spectre.Console is used. Commit: `docs: add third-party notices`.

**T10.4 Changelog and release notes.** Files: `CHANGELOG.md`. Do: Keep a Changelog format; first entry lists M1 to P9 features. Commit: `docs: add changelog`.

**T10.5 Repository hygiene.** Files: `.github/dependabot.yml`. Do: weekly updates for `nuget` and `github-actions`; confirm every action in all workflows is pinned by SHA (add a test script `scripts/ci/check-pins.sh` that fails on `uses: x@v1` style references). Commit: `ci: add dependabot and pin check`.

**T10.6 Migration guide.** Files: `docs/MIGRATION.md`. Do: option map from D.1 (identical short flags), what changed (exit code 2 for usage errors, collisions refused, `-y` runs winget or Homebrew instead of apt, drivers are never installed, `VNEAT_NETWORK` dropped, `VIDEO2MP4_NETWORK` dropped, hidden files skipped, order is case-insensitive). Commit: `docs: add migration guide`.

**T10.7 Final README.** Files: `README.md`. Do: all sections of H.1 complete; install one-liners use the tagged installer URL (not `main`); verify every command in the README by running it on a clean machine or CI job [V]. Commit: `docs: finalize README`.

**T10.8 Optional package manifests (owner decision).** winget, Homebrew and Scoop manifests for Drawplate itself. Do not start without approval.

## G. Installer specification (`scripts/dist/install.sh` and `scripts/dist/install.ps1`)

### G.1 Contract

Both scripts install, upgrade, repair or uninstall **Drawplate itself** in user space. They never need elevation, never install FFmpeg (they offer `drawplate setup` at the end) and never change anything outside the Drawplate folders and the user PATH.

- **One-liners (always pinned to a release tag, never `main`):** Linux and macOS `curl -fsSL https://raw.githubusercontent.com/migueljocode/Drawplate/<tag>/scripts/dist/install.sh | sh`; with options `... | sh -s -- --version v1.2.3`. Windows `irm https://raw.githubusercontent.com/migueljocode/Drawplate/<tag>/scripts/dist/install.ps1 | iex`; with options `& ([scriptblock]::Create((irm <url>))) -Version v1.2.3`.
- **Options:** `--version <tag>` / `-Version`; `--dir <folder>` / `-Dir`; `--no-setup` / `-NoSetup`; `--yes` / `-Yes`; `--uninstall` / `-Uninstall`; `--from <archive>` with `--sha256 <hex>` / `-From` with `-Sha256` (offline install; the hash is mandatory); `--help` / `-Help`. Piped runs (`| sh`, `| iex`) read the same settings from the environment: `DRAWPLATE_VERSION`, `DRAWPLATE_INSTALL_DIR`, `DRAWPLATE_REPO` (default `migueljocode/Drawplate`), `DRAWPLATE_BASE_URL` (default `https://github.com/<repo>/releases`; for mirrors and tests), `NO_COLOR`.
- **Release layout:** `<base>/download/<tag>/drawplate-<version>-<rid>.tar.gz` (Unix) or `.zip` (Windows) and `<base>/download/<tag>/SHA256SUMS` (lines `<hash>  <file>`). Latest: HEAD `<base>/latest`, take the tag from the `Location` header (no API calls, no rate limits). `<version>` is the tag without `v`.
- **rid:** Linux x86_64 `linux-x64`; Linux aarch64 or arm64 `linux-arm64`; macOS arm64 `osx-arm64`; Windows AMD64 `win-x64`. Anything else (including Intel macOS in v1): I-001, exit 11.
- **Locations:** Unix binary `$HOME/.local/bin/drawplate`, manifest `$HOME/.local/share/drawplate/install.json`. Windows binary `%LOCALAPPDATA%\Programs\Drawplate\drawplate.exe`, manifest `%LOCALAPPDATA%\Drawplate\install.json`. `--dir` overrides the binary folder.
- **Manifest** (UTF-8, no BOM): `{"version":"1.2.3","rid":"linux-x64","archiveSha256":"<hex>","binarySha256":"<hex>","installedAt":"2026-10-31T12:00:00Z","path":"<binary path>"}`.
- **Exit codes:** 0 ok; 10 usage error; 11 unsupported platform; 12 download failed; 13 checksum mismatch; 14 install failed (including the post-install check); 15 another install is running; 16 blocked by PowerShell policy.
- **Messages** (stderr; `Error: <text>` then `Hint: <text>`):
  I-001 `Unsupported platform: {os} {arch}.` Hint `Supported: linux-x64, linux-arm64, osx-arm64, win-x64.`
  I-002 `Could not find a release for {repo} ({detail}).` Hint `Check your internet connection, or set DRAWPLATE_VERSION.`
  I-003 `Download failed: {url} ({detail}).` Hint `Retry, or download the archive yourself and use --from <archive> --sha256 <hash>.`
  I-004 `Checksum mismatch for {file}.` Hint `Nothing was installed. Retry; if it repeats, report it.`
  I-005 `Could not install to {path} ({detail}).` Hint `Choose another folder with DRAWPLATE_INSTALL_DIR.`
  I-006 `The installed program did not start ({detail}). The previous version was restored.`
  I-007 `Another installation is running (lock: {path}).` Hint `Wait for it, or delete that folder if no installer is running.`
  I-008 `This PowerShell session blocks scripts ({detail}).` Hint `Run: powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`
  I-009 (Unix, stdout) `Note: {dir} is not on your PATH.` then `  Add this line to your shell profile:  export PATH="$HOME/.local/bin:$PATH"`
  I-010 (Windows, stdout) `Your user PATH was updated. Open a new terminal to use 'drawplate'.`
  I-011 (stdout) `FFmpeg was not found on this machine.`

### G.2 Display contract (progress and steps)

All output is ASCII. Colors (`[ok]` green, `[skipped]` yellow, `[failed]` red) only when stdout is a terminal and `NO_COLOR` is unset.

Header line: `Drawplate installer <version>`. Install steps (N = 8):
`1 Detecting platform`, `2 Finding release`, `3 Downloading Drawplate`, `4 Downloading checksums`, `5 Verifying checksum`, `6 Installing`, `7 Checking the install`, `8 Finishing`.
Uninstall steps (N = 4): `1 Locating installation`, `2 Removing files`, `3 Updating PATH`, `4 Finishing`.

**Step line** (one line, written when the step completes; title padded with dots to column 30): `[<n>/<N>] <Title> ..... [ok] <detail>`, for example:
```
[1/8] Detecting platform ..... [ok] linux-x64
[2/8] Finding release ........ [ok] v1.2.3
```
**Download steps (3 and 4)** print their header at the start, then the progress line, and end with the final progress line (they print no `[ok]`):
```
[3/8] Downloading Drawplate (6.0 MiB)
      [########------------] 40%  2.4 / 6.0 MiB  1.4 MiB/s  eta 0:03     (rewritten in place with CR, at most 5 times a second)
      [####################] 100%  6.0 / 6.0 MiB  1.4 MiB/s  4s          (final line, ends with a newline)
```
Unknown total size: `      2.4 MiB  1.4 MiB/s`. Not a terminal: no rewriting; print `      25%`, `      50%`, `      75%` and finally `      100%  6.0 MiB in 4s`; with unknown total print a line every 5 MiB and a final `      <size> MiB in <t>s`. The bar is 20 cells of `#` and `-`. Speed is total bytes divided by elapsed seconds. A failed download prints a `[failed]` line instead of the final progress line.
**Skipped steps** (already up to date): steps 3 to 7 each print `[skipped] up to date`. **Repair:** step 6 prints `[ok] repairing`.
**Failure:** the step prints `[failed]`, then `Error:` and `Hint:` on stderr, then the process exits with the code of G.1.
**Final block (success):**
```

Installed Drawplate 1.2.3 to /home/u/.local/bin/drawplate (11s)
Next:
  drawplate --help
  drawplate setup        (shows how to install FFmpeg)
```
followed by I-009 or I-010 when it applies, then I-011 plus the offer `Run 'drawplate setup' now? [Y/n]` when FFmpeg is not on PATH, a terminal is attached and neither `--no-setup` nor `--yes` is given (`--yes` runs `drawplate setup` directly; `--no-setup` only prints the hint). Uninstall final line: `Removed Drawplate. FFmpeg was not touched.`

### G.3 Algorithm (identical in both scripts)

1. Parse options and environment; unknown option: exit 10 with usage.
2. Detect the platform; build `rid`. (Windows only: if `$ExecutionContext.SessionState.LanguageMode` is not `FullLanguage`, I-008, exit 16.)
3. Take the lock: create the folder `<data folder>/drawplate/.lock` (atomic create). If it exists and is younger than 30 minutes: I-007, exit 15; if older: remove it and continue. Register cleanup for the lock and the staging folder on every exit path (`trap` in sh; `try/finally` in PowerShell).
4. Resolve the tag (option, environment, or the `latest` redirect). Failure: I-002, exit 12.
5. Up-to-date check: manifest exists and its `version` equals the target and the binary exists and its SHA-256 equals `binarySha256` and `<binary> --version` prints `drawplate <version>`: mark steps 3 to 7 skipped and go to step 8 of the display.
6. Staging folder `<binary folder>/.drawplate-staging-<pid>` (same file system, so the final rename is atomic; stale ones older than 1 hour are deleted first). Download the archive and `SHA256SUMS` into it (with `--from`, copy instead and use the given hash). Find the line for the archive name in `SHA256SUMS` (missing: I-004). Compute the archive SHA-256 and compare (mismatch: I-004, exit 13, delete staging, touch nothing else).
7. Extract into `staging/x`; the archive must contain only `drawplate` (or `drawplate.exe`), `LICENSE` and `THIRD-PARTY-NOTICES.md` (anything else: abort with I-005). Set the executable bit on Unix.
8. Replace: if a binary exists, rename it to `<name>.old`; move the new binary into place; run `<binary> --version` (10 s timeout) and compare with `drawplate <version>`; on any failure restore `.old` (I-006, exit 14); on success delete `.old`.
9. Write the manifest to a temporary file and rename it over the old one.
10. PATH: Unix, test `":$PATH:"` for the folder and print I-009 when missing (never edit profile files). Windows: read the **user** PATH with `[Environment]::GetEnvironmentVariable('Path','User')`; if the folder is missing (compare case-insensitively without trailing separators) append it with `SetEnvironmentVariable('Path', <new>, 'User')`, update `$env:Path` for the current session, print I-010.
11. FFmpeg offer (G.2). Print the final block. Exit 0.
Uninstall: locate through the manifest (or the default path), delete the binary, `.old`, the manifest and empty parent folders created by the installer, remove the folder from the Windows user PATH, never touch FFmpeg.

### G.4 Installer test cases (run for both scripts; `scripts/tests/serve_release.py` is a 60-line local server bound to `127.0.0.1` with `latest` redirect, file serving and fault modes `truncate`, `badsums`, `slow`)

| ID | Scenario | Expected |
|---|---|---|
| T-INS-01 | Fresh install from the local server | Exit 0; eight step lines each `[ok]` or a progress block; manifest present; `drawplate --version` works; I-009 or I-010 printed |
| T-INS-02 | Run again, same version | Exit 0; steps 3 to 7 `[skipped] up to date`; no download requests reach the server |
| T-INS-03 | Truncate the installed binary, run again | Exit 0; step 6 `[ok] repairing`; binary hash equals the manifest again |
| T-INS-04 | Server returns wrong `SHA256SUMS` | Exit 13; I-004; no staging folder left; the previous install untouched |
| T-INS-05 | Server cuts the download | Exit 12 or 13; nothing installed; second run succeeds |
| T-INS-06 | Archive holds a non-working binary | Exit 14; I-006; previous version restored |
| T-INS-07 | Lock folder younger than 30 minutes exists | Exit 15; I-007 |
| T-INS-08 | Output without a terminal | Progress lines at 25, 50, 75 and 100 percent; no CR characters; no byte above 0x7F; no ANSI |
| T-INS-09 | `--uninstall` | Files and manifest removed; Windows user PATH entry removed; exit 0; FFmpeg untouched |
| T-INS-10 | Windows only: same scenario under `powershell` and `pwsh` | Step lines identical after normalizing paths and times |
| T-INS-11 | Windows only: source scan | `install.ps1` has no non-ASCII byte and none of the forbidden constructs of G.5 |
| T-INS-12 | `latest` resolution through the redirect | Chosen tag equals the server's latest |

### G.5 PowerShell rules (5.1 and 7+ in one file)

- Start with `Set-StrictMode -Version 2.0`, `$ErrorActionPreference = 'Stop'`, `$ProgressPreference = 'SilentlyContinue'` (we draw our own progress).
- **Never use:** `?:`, `??`, `??=`, `?.`, `?[]`, `&&`, `||`, `ForEach-Object -Parallel`, `-AsHashtable`, `-SkipHttpErrorCheck`, `Get-Error`, `$IsWindows`, `$IsLinux`, `$IsMacOS`, `Join-Path` with more than two parts, `Start-Process -ArgumentList` with an array, `Out-File -Encoding utf8` or `Set-Content -Encoding UTF8` for files we author, `Invoke-WebRequest` for downloads.
- Platform: `[Environment]::OSVersion.Platform -eq 'Win32NT'`; edition: `$PSVersionTable.PSEdition`; version: `$PSVersionTable.PSVersion.Major`.
- TLS: when the edition is not `Core`, add TLS 1.2: `[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12`.
- Downloads: `Add-Type -AssemblyName System.Net.Http`; `HttpClient` with `ResponseHeadersRead`, 30-minute timeout, an 81920-byte copy loop that feeds the progress renderer, disposed in `finally`; set `[System.Net.WebRequest]::DefaultWebProxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials` before creating the client. The `latest` lookup uses a second handler with `AllowAutoRedirect = $false` and a HEAD request.
- Hashes: `Get-FileHash -Algorithm SHA256 -LiteralPath`. Extraction: `Add-Type -AssemblyName System.IO.Compression.FileSystem`, `[IO.Compression.ZipFile]::ExtractToDirectory`, then verify the extracted file list (G.3 step 7).
- Starting the program for the post-install check: `System.Diagnostics.ProcessStartInfo` with one `Arguments` string, `RedirectStandardOutput`, `WaitForExit(10000)`; read `ExitCode` explicitly.
- Files we write: `[IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))`. Paths: `[IO.Path]::Combine`, `-LiteralPath`, `$env:LOCALAPPDATA`; never `~`.
- Piped execution: all logic lives in a function `Invoke-DrawplateInstall` that **returns** the exit code. At the end: when `$PSCommandPath` is set, `exit $code`; otherwise (pipeline or `iex`) set `$global:LASTEXITCODE = $code` and `return`, **never `exit`**, because `exit` would close the user's window.
- Output through one `Write-Ui` function (uses `[Console]::Out.Write` when available, else `Write-Host`); CR rewriting only when `-not [Console]::IsOutputRedirected`.

### G.6 Module order for the generator

`00-header`, `10-ui`, `20-platform`, `30-net`, `40-verify`, `50-install`, `60-path`, `70-setup-offer`, `80-uninstall`, `90-main` (extensions `.sh` and `.ps1`). The generated header lists the modules and the commit.

## H. README and documentation specification

### H.1 README sections, in this order
1 What it is (three lines). 2 Install (Windows and Linux/macOS one-liners pinned to the latest tag; what the installer does; progress and steps; update = run again; repair = run again; uninstall). 3 Quick start (five examples taken from the option table). 4 FFmpeg (how Drawplate finds it; the first suggestion is `winget install --id Gyan.FFmpeg -e` on Windows, `brew install ffmpeg` on macOS, your package manager on Linux; `drawplate setup`, `drawplate doctor`, `--ffmpeg-path`, `DRAWPLATE_FFMPEG`). 5 Options (table generated from the option table). 6 Hardware acceleration (drivers are yours; what the note means; `drawplate -l`). 7 Troubleshooting FFmpeg (H.2). 8 Exit codes. 9 Environment variables. 10 Migrating from vneat (link). 11 Building from source. 12 License and FFmpeg's license.

### H.2 "Troubleshooting FFmpeg": required entries (exact headings; content as listed)
- `### winget is not recognized`: winget (App Installer) ships with Windows 10 1809 or newer and Windows 11; Windows Server and some LTSC editions lack it. Install App Installer from the Microsoft Store, open a new terminal, run `winget --version`. Without winget use the manual install: download the Gyan full build zip (releases of `github.com/GyanD/codexffmpeg`), compare its SHA-256 with the value `winget show Gyan.FFmpeg` prints, extract it, then use `--ffmpeg-path <folder>\bin\ffmpeg.exe` or set `DRAWPLATE_FFMPEG`, or add the `bin` folder to your user PATH.
- `### winget asks about source agreements`: the first use may ask you to accept terms; Drawplate's command already passes `--accept-source-agreements --accept-package-agreements`; add both when running winget yourself.
- `### No package found matching input criteria`: run `winget source update`, check the connection, try `winget search ffmpeg`; the id is `Gyan.FFmpeg`.
- `### FFmpeg installed but Drawplate cannot find it`: the terminal reads PATH when it starts, so open a new terminal; winget links programs in `%LOCALAPPDATA%\Microsoft\WinGet\Links`, which Drawplate also checks; run `drawplate doctor` to see where it looks.
- `### Downloads are blocked by a proxy or company policy`: winget uses the system proxy; ask your administrator, or download the zip on another machine and use the manual install.
- `### Antivirus removed ffmpeg.exe`: restore it from quarantine only if the zip's SHA-256 matches the published value; exclude the FFmpeg folder only if you trust the source.
- `### More than one FFmpeg is installed`: list them (`where ffmpeg` on Windows, `which -a ffmpeg` on Unix); the first on PATH wins; choose one explicitly with `--ffmpeg-path`.
- `### The download is large`: the Gyan full build is a 245.8 MiB download and about 650 MB on disk (three programs of about 217 MB each).
- `### Homebrew is not installed (macOS)`: install it from https://brew.sh; on Apple silicon run `eval "$(/opt/homebrew/bin/brew shellenv)"`; then `brew install ffmpeg`.
- `### Linux: package manager commands`: apt `sudo apt install ffmpeg`; pacman `sudo pacman -S ffmpeg`; Fedora needs RPM Fusion for libx264 (two commands from D.5); openSUSE needs Packman. No administrator rights: download a static Linux build from the releases of `github.com/BtbN/FFmpeg-Builds`, verify it with that release's `checksums.sha256`, extract it, use `--ffmpeg-path <folder>/bin/ffmpeg` (needs glibc 2.28 or newer).
- `### This FFmpeg has no libx264`: you have a minimal or LGPL build (or Fedora's `ffmpeg-free`); install the full build (Windows `Gyan.FFmpeg`, macOS Homebrew, Fedora RPM Fusion, openSUSE Packman); `drawplate doctor` shows whether `libx264` is present.
- `### GPU encoding is not used`: Drawplate uses the GPU only when the vendor driver provides an encoder and never installs drivers; run `drawplate -l`; update the driver from NVIDIA, Intel or AMD; on Linux VAAPI needs a media driver and access to `/dev/dri/renderD*` (membership of the `render` group); the note printed by Drawplate is informational.
- `### FFmpeg is too old`: upgrade with `winget upgrade --id Gyan.FFmpeg -e`, `brew upgrade ffmpeg` or your package manager; the minimum is 6.0.

### H.3 Other documents
`docs/FFMPEG.md`: how FFmpeg is found (D.5 order), the facts about `Gyan.FFmpeg` (portable zip, command aliases, sizes, SHA-256 in the manifest), manual installation, checksum verification, license note (GPL builds; Drawplate does not distribute FFmpeg). `docs/HARDWARE.md`: results of T6.7. `docs/MIGRATION.md`: T10.6. `docs/VNEAT-BASELINE.md`: T0.1. `docs/decisions/*`: ADRs.

## I. Appendices

### I.1 Golden argument vectors (captured from Bash `vneat` 2.0.0, Linux, ffmpeg 6.1.1)

Golden files hold **one argv element per line**. `SRC` and `PARTIAL` are placeholders. Below, elements are shown space-separated; the filter is always one element.

CPU, quality 50, no cap:
`-nostdin -hide_banner -loglevel error -nostats -progress pipe:1 -i SRC -map 0:v:0 -map 0:a:0? -sn -dn -vf scale=trunc(iw/2)*2:trunc(ih/2)*2 -c:v libx264 -crf 26 -preset medium -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart -f mp4 -y PARTIAL`
Filter with a cap `hd` (for `sd` use 854 and 480 in place of 1280 and 720; for `fullhd` 1920 and 1080; for `4k` 3840 and 2160; always `w` box first and `h` box second as shown):
`scale=w='if(gt(a,1),min(iw,1280),min(iw,720))':h='if(gt(a,1),min(ih,720),min(ih,1280))':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2`
Encoder segments (replace `-c:v libx264 -crf 26 -preset medium -pix_fmt yuv420p`; `C` = CRF equivalent; `Q` = quality):
- NVENC: `-c:v h264_nvenc -rc vbr -cq C -b:v 0 -pix_fmt yuv420p`
- QSV: `-c:v h264_qsv -global_quality C -pix_fmt nv12`
- VideoToolbox (read from the script, not yet captured): `-c:v h264_videotoolbox -q:v Q -profile:v high -pix_fmt yuv420p`
- VAAPI and AMF: **not captured yet**; T6.1 captures them. Do not guess.
Probe vector (per encoder, with that encoder's segment): `-nostdin -hide_banner -loglevel quiet -f lavfi -i color=c=black:s=256x256:d=0.1 -vf scale=trunc(iw/2)*2:trunc(ih/2)*2 <segment> -frames:v 1 -f null -`
Banner vector: `-nostdin -hide_banner -i SRC`.

### I.2 Fixtures (generated at test time; every clip is 1 second)

```
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=1280x720:rate=30:duration=1 -f lavfi -i sine=frequency=440:duration=1 -c:v libx264 -c:a aac -shortest ok-landscape.mp4
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=1280x720:rate=30:duration=1 -f lavfi -i sine=frequency=330:duration=1 -c:v mpeg4 -c:a mp2 -shortest "Space Name.MOV"
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=720x1280:rate=30:duration=1 -c:v libx264 portrait.mkv
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=160x120:rate=30:duration=1 -c:v libvpx -b:v 200k tiny.webm
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=321x241:rate=30:duration=1 -c:v mpeg4 odd.avi
ffmpeg -y -loglevel error -f lavfi -i sine=frequency=440:duration=1 -c:a aac audio-only.mp4
bad.mp4        4096 random bytes
notes.txt      the text "hi"
-weird.mp4     copy of ok-landscape.mp4 (leading dash; pass after --)
q'uote $(touch PWNED) `id`.mp4   copy of ok-landscape.mp4 (assert that no file PWNED appears)
x/clip.mp4 and y/clip.mov        copies of two different fixtures (collision)
```
Folder fixture for the CLI test: `ok-landscape.mp4`, `Space Name.MOV`, `portrait.mkv`, `tiny.webm`, `odd.avi`, `bad.mp4`, `notes.txt` and a subfolder `sub/` (ignored). Expected: five converted, `bad.mp4` failed, exit 1. (`audio-only.mp4` and the special names are used by the individual regression tests.)

### I.3 Regression tests derived from vneat (all measured on Bash `vneat`)

| Id | Test name | Expectation |
|---|---|---|
| R-T1 | `Reg_FolderMode_OneBadFile_OthersContinue` | five outputs, one failure listed, exit 1 |
| R-T2 | `Reg_Rerun_SkipsExisting` | zero ffmpeg conversions on the second run |
| R-T3 | `Reg_SizeCap_Sd` | 1280x720 gives 852x480; 720x1280 gives 480x852; 160x120 unchanged |
| R-T4 | `Reg_QualityMap_CrfTag` | the `crf=` tag in the output equals 1, 2, 14, 26, 39, 51 for q 100, 99, 75, 50, 25, 1 (read with `strings`-like scan of the first 4 KiB) |
| R-T5 | `Reg_Collision_FailsFast` | **changed from vneat:** exit 2, no ffmpeg started |
| R-T6 | `Reg_OutputEqualsInput_AllSkipped` | every source skipped as already in the output folder, exit 0 |
| R-T7 | `Reg_UnsupportedOnly_And_MissingFile` | exit 1; W-01 for the missing file; N-01 for the unsupported one |
| R-T8 | `Reg_Signals` | SIGINT to the process group and SIGTERM to the PID alone both give exit 130 within 5 s, no ffmpeg left, no partial left |
| R-E1 | `Reg_LeadingDash_AfterDoubleDash` | converts; no stray error output |
| R-E2 | `Reg_HostileFilename_NoInjection` | converts; file `PWNED` does not exist |
| R-E3 | `Reg_AudioOnly_NoVideoStream` | **changed:** reason `no video stream`, exit 1 |
| R-E4 | `Reg_OutputPathIsFile` | **changed:** E-USAGE-08, exit 2 |
| R-E5 | `Reg_FilesMode_DefaultOutput` | `./compressed` created in the current folder |
| R-E6 | `Reg_Piped_NoAnsi` | no byte 0x1B in the output |

### I.5 Interfaces (target C#; names are binding, bodies are the task's job)

```csharp
// Models
namespace Drawplate.Models.Cli;
public enum ExitCode { Ok = 0, Failure = 1, UsageError = 2, FfmpegUnavailable = 3, Interrupted = 130 }

namespace Drawplate.Models.Encoding;
public enum OperatingSystemKind { Windows, Linux, MacOS }
public enum EncoderKind { Cpu, Nvenc, Qsv, Vaapi, Amf, VideoToolbox }
public enum SizeCap { None, Sd, Hd, FullHd, Uhd4K }

public static class QualityScale
{
    public const int Default = 50;
    public static bool IsValid(int q) => q is >= 1 and <= 100;
    public static int ToCrf(int q) => IsValid(q) ? 51 - q / 2 : throw new ArgumentOutOfRangeException(nameof(q));
}

public static class FfmpegArgv
{
    public static ImmutableArray<string> Convert(string source, string partial, EncoderKind encoder, int quality, SizeCap cap) =>
    [
        "-nostdin", "-hide_banner", "-loglevel", "error", "-nostats", "-progress", "pipe:1",
        "-i", source, "-map", "0:v:0", "-map", "0:a:0?", "-sn", "-dn",
        "-vf", SizeCaps.VideoFilter(cap),
        .. EncoderProfiles.Args(encoder, quality),
        "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", "-f", "mp4", "-y", partial,
    ];
}

namespace Drawplate.Models.Setup;
public sealed record PlatformInfo(OperatingSystemKind Os, string Architecture, ImmutableHashSet<string> Tools,
                                  string? LinuxId, string? LinuxIdLike, int? FedoraRelease);
public sealed record InstallSuggestion(string Title, ImmutableArray<string> DisplayLines,
                                       string? Executable, ImmutableArray<string> Arguments, bool RunnableByApp);

// Services
namespace Drawplate.Services.Processes;
public sealed record ProcessOptions(TimeSpan? Timeout = null, bool InheritConsole = false);
public interface IRunningProcess : IAsyncDisposable
{
    IAsyncEnumerable<string> StdoutLines { get; }
    string StderrTail { get; }                  // ring buffer, last 64 KiB
    bool TimedOut { get; }
    Task<int> WaitAsync(CancellationToken ct);  // on cancel: Kill(entireProcessTree: true), then throw
}
public interface IProcessLauncher
{
    IRunningProcess Start(string executable, IReadOnlyList<string> arguments, ProcessOptions? options = null);
}
```

### I.6 Templates

PROGRESS.md entry:
```
### <date> <task id> <title>
Result: DONE | BLOCKED
[V] <command you ran> -> <result> (local | CI run URL)
[I] <inference and why>
[U] <unknown and what would settle it>
```
ADR (`docs/decisions/NNNN-title.md`): `Status`, `Context`, `Decision`, `Alternatives`, `Consequences`.
