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

## Repository layout

See plan.md section C.1.

## Standard verification commands

- STD-BUILD: `dotnet build src/Drawplate.slnx -c Release` -> Expected: `0 Warning(s)`, `0 Error(s)`.
- STD-FORMAT: `dotnet format src/Drawplate.slnx --verify-no-changes` -> Expected: exit code 0.
- STD-TEST: `dotnet test tests/Drawplate.Tests -c Release` -> Expected: `Failed: 0`.

## Matrix ids

Comments explaining non-obvious rules carry matrix ids (for example `// F06`).
