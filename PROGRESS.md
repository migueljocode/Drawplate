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
Result: **DONE** (repair and CI completion recorded in the R19 plan snapshot).
[I] Main CI run https://github.com/migueljocode/Drawplate/actions/runs/43062731366 is recorded as successful on `main` at `c240e2c` (Ubuntu, macOS, Windows).
[I] Installer CI run https://github.com/migueljocode/Drawplate/actions/runs/43059860922 is recorded as successful on R18 tip `dd33b21`, including both Windows shells.
[I] The Windows `System.Object[]` exit-code failure was traced to `$response.EnsureSuccessStatusCode()` emitting its returned `HttpResponseMessage` from `Get-HttpFile`; suppressing that non-contract output with `[void]` in source and generated distribution fixed the exit-code propagation.
[U] These historical workflow runs were not independently re-fetched in this execution session; see the linked run records for the CI evidence.

### 2026-11-11 R19 Documentation and release engineering
Result: **IN PROGRESS** (this branch; see plan.md).

## Unverified

## Blocked

## Decisions
- 2026-11-11: Owner treats R17 as DONE; agent proceeds with R18 only.
- 2026-11-11: R18 marked DONE from the CI evidence recorded in plan.md; historical run records remain independently re-checkable.

## Deferred
