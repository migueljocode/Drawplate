# Plan — R18 Installer scripts (finish)

**Status:** R17 treated as DONE by owner (2026-11-11). This file is the **only** stage the actor may execute. Do not start R19. Do not edit product `src/`.

**Evidence still open:** B25 (installers). Spec: section G of the full historical plan (binding); G.4 T-INS-01..12; G.5 PowerShell rules.

**Current state [V from PROGRESS on this branch]:**
- Generator copies single-file sources into `scripts/dist/` (no ten-module list).
- `installers.yml` exists; Ubuntu/macOS jobs green.
- Windows Pester under both PowerShell 5.1 and 7: T-INS-04, T-INS-05, T-INS-06 still report exit 0 instead of 13 / 12-or-13 / 14 even though the error messages and rollback behaviour appear in the log.
- Shared cause already identified: exit-code propagation when the script is invoked as a file vs piped/`iex`. Source already ends with:
  ```
  $exitCode = Invoke-DrawplateInstall
  if ($PSCommandPath) { exit $exitCode }
  $global:LASTEXITCODE = $exitCode
  return
  ```
  Remaining work is to make the **Pester helpers** and any remaining dispatch path return those codes reliably, and to prove cleanup (no staging left, `.old` restored).

## Reviewer Briefing
- HIGH: T-INS-04 (bad checksum → 13), T-INS-05 (truncated download → 12 or 13), T-INS-06 (non-working binary → 14 + previous restored) must return the exact exit codes on **both** Windows shells. Message-only success is not enough.
- HIGH: After any of those failures the staging folder must be gone and, for T-INS-06, the previous binary must be restored (`.old` moved back).
- MEDIUM: `git diff --exit-code scripts/dist` after regenerating must be clean (dist is committed and current).
- MEDIUM: Never install system packages (no `apt`/`brew`/`winget`/`choco`). Local `serve_release.py` only. AGENTS rule 8.
- LOW: ShellCheck / PSScriptAnalyzer already expected green; do not regress.

## [ ] Step 1 — Reproduce and pin the three Windows failures
- Files: inspect only — `scripts/src/ps1/install.ps1`, `scripts/tests/ps1/install.Tests.ps1` (and any helper), `scripts/tests/serve_release.py`, CI log of run 43005858724.
- Symbols: `Invoke-DrawplateInstall`, `$PSCommandPath`, `$global:LASTEXITCODE`, Pester wrapper that launches the installer.
- Current → Desired: failures return 0 → return 13 / 12-or-13 / 14 and leave no staging / restore `.old`.
- Change: **none yet**. Record the exact Pester assertion text and the process exit code observed.
- Depends on: none (R17 done).
- Edge cases: invocation via `-File`, via `iex`, via Pester's `Start-Process` / `&` / script-block.
- Tests: the existing T-INS-04/05/06.
- Verify:
  ```
  # On a Windows runner or local pwsh 5.1 + 7, against the local server:
  # run only the three failing tests and capture $LASTEXITCODE / process ExitCode
  ```
- Done when: each of the three tests has a one-line note of (a) how the installer is launched, (b) observed exit code, (c) whether staging/`.old` remained.
- Risk: LOW
- Confidence: HIGH

## [ ] Step 2 — Fix exit-code propagation and cleanup assertions
- Files: `scripts/src/ps1/install.ps1` (only if the `$PSCommandPath` path is still wrong), `scripts/tests/ps1/install.Tests.ps1` (and helpers), optionally `scripts/tests/serve_release.py` fault modes (`badsums`, `truncate`, bad binary).
- Symbols: same as Step 1; G.5 forbids `?:` `??` `&&` `||` `$IsWindows` etc.
- Current → Desired: every failure path of `Invoke-DrawplateInstall` returns the documented code; the test harness captures that code; staging is always removed in `finally`; T-INS-06 restores the previous binary.
- Change: make the Pester launcher use a form that surfaces `exit` / `$LASTEXITCODE` (prefer `-File` + check process ExitCode, or set `$global:LASTEXITCODE` and read it). Strengthen assertions: after failure, `Test-Path staging` is false; for T-INS-06 the original binary content/hash is back.
- Depends on: Step 1 notes.
- Edge cases: PowerShell 5.1 vs 7; `iex` path still sets `$global:LASTEXITCODE` for completeness.
- Tests: T-INS-04, T-INS-05, T-INS-06 must pass on both shells; no other T-INS may regress.
- Verify:
  ```
  # Installer CI (or local):
  # both Windows jobs: all T-INS-* Passed
  # Ubuntu/macOS shell tests still green
  ```
- Done when: Installer CI run is fully green (all five jobs).
- Risk: HIGH
- Confidence: MEDIUM

## [ ] Step 3 — Dist current + final sign-off
- Files: `scripts/build-installers.ps1`, `scripts/dist/install.sh`, `scripts/dist/install.ps1`.
- Current → Desired: regenerate → `git diff --exit-code scripts/dist` is empty; dist committed if anything changed.
- Change: run generator; commit only if dist differs.
- Depends on: Step 2 green.
- Verify:
  ```
  pwsh -NoProfile -File scripts/build-installers.ps1
  git diff --exit-code scripts/dist
  ```
- Done when: diff clean; Installer CI still green on the tip commit.
- Risk: LOW
- Confidence: HIGH

## Global Definition of Done
- All G.4 tests (T-INS-01..12) pass, including the three Windows exit-code cases.
- `installers.yml` fully green.
- `scripts/dist/` matches sources after generator run.
- No system package installs performed.
- PROGRESS.md: R17 remains DONE; R18 marked DONE with [V] CI run link and the exact commands.
- One commit per step; stop after each for Job B. Actor never edits plan.md / audit / ticks stages.

## Execution notes
- One step → one commit → STOP.
- Scope = R18 only. No README (R19), no release.yml (R19), no product code.
- If a new root cause appears outside the three tests, STOP and report; do not expand scope.
