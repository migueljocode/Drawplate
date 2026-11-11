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
[I] `$PSCommandPath` / `$LASTEXITCODE` dispatch already present in `scripts/src/ps1/install.ps1`; remaining work is harness capture + cleanup assertions (see plan.md Step 1–3).
[U] Final green Installer CI run required before marking R18 DONE.

## Unverified

## Blocked

## Decisions
- 2026-11-11: Owner treats R17 as DONE; agent proceeds with R18 only.

## Deferred
