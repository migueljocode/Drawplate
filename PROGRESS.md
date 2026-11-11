# Progress

## Verified

### 2026-11-05 All phases P0-P10 complete
Result: DONE (historical claim only; superseded by repair stages R01–R20). The original “APPROVED” / complete language written inside implementing commits is not authoritative.

## Repair stages (plan.md Part 4)

### R01–R16
Result: **DONE** (see prior entries on main / earlier task branches).

### 2026-11-11 R17 Native AOT, CI smoke, size budget
Result: **DONE** (owner direction 2026-11-11: treat as complete).
[V] CI run https://github.com/migueljocode/Drawplate/actions/runs/43000273712 — Ubuntu, macOS and Windows all green.
[V] Native-AOT publish and framework-dependent baseline for linux-x64, osx-arm64, win-x64; no publish warnings treated as errors.
[V] Exact version check, one-second FFmpeg conversion smoke, Unix SIGTERM exit-130 smoke passed.
[V] Per-RID sizes recorded in workflow job summaries.
[U] T1.6/S5 UI-library cost spike unmeasured (no SDK in authoring environment); ADR 0003 records the limit; no UI package added.

### 2026-11-11 R18 Installer scripts
Result: **DONE** (merged to main @ `c240e2c`, PR #17).
[V] Main CI https://github.com/migueljocode/Drawplate/actions/runs/43062731366 on `c240e2c` — Ubuntu, macOS, Windows green.
[V] Installer CI https://github.com/migueljocode/Drawplate/actions/runs/43059860922 on R18 tip `dd33b21` — all jobs including both Windows shells; T-INS exit codes fixed.
[V] Root cause of Windows exit 1 was `Invoke-DrawplateInstall` emitting `System.Object[]` (HttpResponseMessage from `EnsureSuccessStatusCode`); fixed with `[void]` so the function returns a single int.

### 2026-11-11 R19 Documentation, honest status, release engineering
Result: **IN PROGRESS** (branch `task/R19-docs`; see plan.md / PR #19).
[V] R18 close-out written in this file.
[V] README: options table, 13-entry Troubleshooting FFmpeg section, no internal plan anchors.
[V] ADR 0002 (project name Drawplate) added; ADR 0001 and 0003 retained (0003 still [U] on measurements).
[V] `.github/workflows/release.yml` (manual dispatch, dry-run, linux-x64/arm64, osx-arm64, win-x64) and `.github/dependabot.yml` added.
[V] CHANGELOG.md annotated that PROGRESS.md is authoritative for repair-stage status.
[U] End-to-end dry-run of release.yml on a fork not executed in this session; first real tag is the verification.
[U] Spike ADRs beyond 0003 (process/cancellation, etc.) not backfilled; recorded only if evidence exists.

### R20 Acceptance on the target machine
Result: **NOT STARTED** (owner manual). Commands and acceptance criteria are in plan.md (R20 section). Results go into `docs/HARDWARE.md`.

## Unverified

- Real VAAPI / hardware encoder behaviour on the owner’s Ubuntu 26.04 + i5-5200U + GeForce 820M machine (R20).
- Native-AOT + Spectre.Console size/startup spike (ADR 0003).
- Windows/macOS behaviour beyond what CI logs show for hardware encoders.

## Blocked

None.

## Decisions
- 2026-11-11: Owner treats R17 as DONE; agent proceeded with R18 only.
- 2026-11-11: R18 marked DONE from CI evidence (runs linked above).
- 2026-11-11: Single R19 branch is `task/R19-docs`; prior `task/R19-docs-release-engineering` closed/deleted.

## Deferred
- R20 owner hardware capture.
- Optional release.yml / Dependabot / CHANGELOG polish if not finished inside R19.
