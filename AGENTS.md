# Drawplate agent rules

This document is the single source of truth for how agents work on Drawplate.

## Sources of truth, in order

1. The plan.md task you were given and the sections it cites
2. AGENTS.md (this file)
3. PROGRESS.md

If two sources disagree, or the task is ambiguous, STOP and ask. Never guess and never choose silently.

## Non-negotiable rules

1. Scope: do only what the task lists. No extra features, refactors, renames, dependencies, or files outside the task's file list. If more seems necessary, STOP and report it as a proposal.
2. Evidence: never claim a command, test or CI run succeeded unless you ran it in this session and saw the result. Label every claim [V] verified (you ran it), [I] inferred, or [U] unknown.
3. Facts: never invent package versions, API names, URLs, hashes, flags or file contents. Look them up (package search, `--help`, source code) or write [U] and ask. Never type a SHA-256 from memory.
4. Tests first: write the tests listed in the task, run them, see them fail for the right reason, then implement until they pass. Never weaken, skip, delete or rename a test to make it pass.
5. Warnings are errors. No NoWarn, no pragma disable, no analyzer suppression. Fix the cause.
6. Architecture: Models has no packages, no IO, no processes. Services never references Spectre.Console. Only Console references Spectre. Do not edit the architecture test to allow a violation.
7. Processes: always `ProcessStartInfo.ArgumentList`. Never build a command-line string. Never use a shell (`sh -c`, `cmd /c`, `powershell -Command` with interpolated data).
8. No elevation: never call `sudo`, `pkexec`, `runas`, or install system packages. Linux install commands are only printed for the user to run.
9. Files: never overwrite or delete files you did not create in this run. Partial files are unique per process and always cleaned up.
10. PowerShell: scripts must run on Windows PowerShell 5.1 and PowerShell 7+. ASCII only. No PowerShell-7-only syntax.
11. Style: follow section C exactly.
12. User-facing text: use only the strings in sections D and E. Do not rephrase them.
13. Commits: one commit per task, format in section C.9. Never commit binaries, media files, secrets or build output.
14. Security: file names are untrusted input. Never interpolate them into shell strings. Sanitize before printing.
15. Waiting: no arbitrary sleeps to hide races. Use the waiting primitives named in the task.

## Working loop (for the single current task)

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

## Engineering conventions

### C.1 Repository layout

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

### C.2 Dependency rules

Models references nothing but the BCL. Services references Models. Console references Models, Services and Spectre.Console. Tests reference all three. These rules are enforced by the architecture test (T1.3).

### C.3 Code style

- `LangVersion latest`, nullable enabled, file-scoped namespaces, one top-level type per file named after the file.
- Each project has `GlobalUsings.cs` containing only `global using` lines. Never put a project's own namespace in `Directory.Build.props`.
- Prefer `record` and `record struct`, primary constructors, collection expressions (`[a, b, ..c]`), switch expressions, pattern matching (`is >= 1 and <= 100`), `required`/`init`, raw string literals, `ImmutableArray<T>` for public collections.
- No `static` mutable state. Time through `TimeProvider`; processes through `IProcessLauncher`; file system access through the abstractions the task names.
- Public API is `internal` by default; `public` only where another project needs it (use `InternalsVisibleTo` only for the test project).
- Comments explain *why* and carry matrix ids (for example `// F06`); no commented-out code.

### C.4 Error handling

Expected failures are values (result records, enums such as `FailureKind`). Exceptions are for programmer errors (`ArgumentException`), cancellation (`OperationCanceledException`) and unexpected IO at the host boundary. `catch (Exception)` is allowed only in `AppHost` (maps to exit 1 with the message) and in best-effort cleanup (comment why). Never swallow cancellation.

### C.5 Output rules

stdout: plan card, per-file lines, summary, `--list-encoders`, `doctor`, notes. stderr: warnings and errors. ANSI colors only when stdout is a terminal and `NO_COLOR` is unset. Unicode glyphs only when the console output encoding is UTF-8 and `TERM` is not `dumb`; otherwise the ASCII table in D.4. Width from the console (80 when unknown). Live progress only on a terminal. Every printed file name goes through `Sanitizer.ForDisplay` (strips control characters and ESC).

### C.6 Process rules

One launcher (`IProcessLauncher`, T3.1). Arguments are lists. stdout is read line by line; stderr goes to a 64 KiB ring buffer. Every probe has a timeout. Cancellation kills the whole process tree. Tests use `FakeExe`, never real `sudo`, `winget` or `brew`.

### C.7 Filesystem, culture, time

`CultureInfo.InvariantCulture` for every parse and format. Paths with `Path.Combine`/`Path.GetFullPath`. Collision and ordering comparisons use `StringComparison.OrdinalIgnoreCase` (never `ToLower()`); ordering falls back to `Ordinal`. UTF-8 without BOM for every file we write. Resolve symlinks before comparing paths.

### C.8 Testing

xUnit. Name: `Subject_Condition_Expected`. Tier trait on every test: `[Trait("Tier","T0")]` unit, `T1` process (FakeExe), `T2` integration (real ffmpeg), `T3` artifact smoke, `T4` static. No network in T0 to T3. Integration tests use `[FfmpegFact]`, which **fails** when FFmpeg is missing and the environment variable `CI` is set, and skips with a message locally. Temp folders come from `TempDir` (deleted on dispose). Media is generated at test time (I.2) and never committed. Golden argument vectors live in `tests/Drawplate.Tests/Golden/*.txt`, one argv element per line, with `SRC` and `PARTIAL` placeholders.

### C.9 Git

Branch `task/<id>-<slug>`. One commit per task, Conventional Commits: `feat(models): add QualityScale (F07)`; scopes `models services console tests ci scripts docs`; the body lists the Verify commands run. Never force-push shared branches. PR title equals the commit subject.

### C.10 Self-review checklist (run before reporting)

1. Only listed files changed.
2. Build has 0 warnings.
3. New tests fail without the implementation and pass with it.
4. No suppressions.
5. No string-built command lines.
6. No new dependency.
7. User-facing strings match D and E exactly.
8. Matrix ids are in comments where rules are non-obvious.
9. No leftover debug output or TODO.
10. PROGRESS.md updated with [V]/[I]/[U].

### C.11 AOT rules

No reflection, no `Assembly.Load`, no `dynamic`. JSON only through a `JsonSerializerContext`. `IsAotCompatible` on Models and Services. Trim and AOT warnings are errors. `InvariantGlobalization` true.

## Repository layout

See plan.md section C.1.

## Standard verification commands

- STD-BUILD: `dotnet build src/Drawplate.slnx -c Release` -> Expected: `0 Warning(s)`, `0 Error(s)`.
- STD-FORMAT: `dotnet format src/Drawplate.slnx --verify-no-changes` -> Expected: exit code 0. (If `.slnx` is not supported by `dotnet format` in the installed SDK, run it per project and record that in PROGRESS.md.)
- STD-TEST: `dotnet test tests/Drawplate.Tests -c Release` -> Expected: `Failed: 0`.
- `TEST <filter>`: `dotnet test tests/Drawplate.Tests -c Release --filter "<filter>"` -> Expected: the named tests are listed as passed and `Failed: 0`.

## Matrix ids

Comments explaining non-obvious rules carry matrix ids (for example `// F06`).
