# Progress

## Verified

### 2026-11-05 All phases P0-P10 complete
Result: DONE (historical claim; superseded by repair stages R01–R20)

## Repair stages (plan.md Part 4)

### R01–R16
Result: DONE (see prior entries on main / earlier task branches).

### 2026-11-11 R17 Native AOT, CI smoke, size budget
Result: **DONE** (owner direction 2026-11-11: treat as complete; forgotten mark fixed here).
[V] CI run https://github.com/migueljocode/Drawplate/actions/runs/43000273712 — Ubuntu, macOS and Windows all green.
[V] Native-AOT publish and framework-dependent baseline for linux-x64, osx-arm64, win-x64; no publish warnings treated as errors.
[V] Exact version check, one-second FFmpeg conversion smoke, Unix SIGTERM exit-130 smoke passed.
[V] Per-RID sizes recorded in workflow job summaries.
[U] T1.6/S5 UI-library cost spike unmeasured (no SDK in authoring environment); ADR 0003 records the limit; no UI package added.

### 2026-11-11 R18 Installer scripts
Result: **IN PROGRESS** (focused plan rewritten on this branch).
[V] Generator copies single-file sources; dist present; Ubuntu/macOS Installer CI green.
[V] Prior Installer CI run https://github.com/migueljocode/Drawplate/actions/runs/43005858724 — Windows shells failed only T-INS-04/05/06 (exit 0 instead of 13/12-or-13/14).
[V] Step 1 reproduction on Installer CI run [43055606701](https://github.com/migueljocode/Drawplate/actions/runs/43055606701): all three cases are launched by the Pester helper with PowerShell `-File` and captured through `System.Diagnostics.Process.ExitCode`; observed exit code is 0 for T-INS-04 (expected 13), T-INS-05 (expected 12/13), and T-INS-06 (expected 14), on both PowerShell 5.1 and 7. Logs show the intended checksum rejection, truncated-response rejection and previous-binary restoration messages. Cleanup checks were previously after the failing code assertion, so their state was not observable; moved them before code assertions and added explicit diagnostics.
[I] Step 2 makes process termination explicit via `[Environment]::Exit`, while retaining `$PSCommandPath` and `$MyInvocation.MyCommand.Path` as file-execution detection and `$global:LASTEXITCODE` for interactive/piped use.
[I] Repair implementation and reordered diagnostics committed in 72388b4; awaiting Installer CI and general CI on the updated branch tip.
[U] Final green Installer CI run required before marking R18 DONE.


[V] Step 2 traced the Windows `System.Object[]` exit failure to `$response.EnsureSuccessStatusCode()` emitting its returned `HttpResponseMessage` from `Get-HttpFile`; suppressed that non-contract output with `[void]` in source and generated distribution. Awaiting both Windows-shell verification.

## Unverified

## Blocked

## Decisions
- 2026-11-11: Owner treats R17 as DONE; agent proceeds with R18 only.

## Deferred
