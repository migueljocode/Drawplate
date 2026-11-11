# Plan — R18 Installer scripts (finish)

**Status:** R17 DONE. Main CI DONE on tip. Actor executes **only** the remaining Windows installer exit-code fix. Do not start R19. Do not edit product `src/`.

## CI snapshot (verified 2026-11-11, tip `1026ff2`)

| Workflow | Result | Notes |
|----------|--------|-------|
| **CI** run [43058175108](https://github.com/migueljocode/Drawplate/actions/runs/43058175108) | **success** | Ubuntu, macOS, Windows all green. Hybrid FFmpeg: `setup-ffmpeg@v3` on Linux/Windows (~6–16 s), `brew install ffmpeg` on macOS (~29 s). No apt hang. |
| **Installer CI** run [43058175175](https://github.com/migueljocode/Drawplate/actions/runs/43058175175) | **failure** | `generated-up-to-date`, `shell-ubuntu`, `shell-macos` **success**. `windows-pwsh-7` and `windows-powershell-51` **failure** on Pester. |

### Windows Pester failures (same on 5.1 and 7)

| Test | Expected | Got | Extra |
|------|----------|-----|-------|
| T-INS-01 install | 0 | **1** | |
| T-INS-03 repair | 0 | **1** | |
| T-INS-04 bad checksum | 13 | **1** | `staging=0` (cleaned) |
| T-INS-05 truncated | 12 or 13 | **1** | `staging=0` |
| T-INS-06 invalid binary | 14 | **1** | `restored=True`, `backup=False`, `staging=0` |
| T-INS-12 latest redirect | 0 | **1** | Install UI ran successfully then crashed on exit path |

**Root cause [V from job log 130653151261]:**

```
InvalidArgument: scripts/dist/install.ps1:388
$exitCode = [int](Invoke-DrawplateInstall)
Cannot convert the "System.Object[]" value of type "System.Object[]" to type "System.Int32".
```

`Invoke-DrawplateInstall` is putting **multiple objects** on the success output stream (typical PowerShell pitfall: `Write-Ui` / host writes that accidentally go to the pipeline, or multiple `return` values). The outer script then fails the `[int]` cast and the process ends with **exit 1**, masking the real codes 0 / 12 / 13 / 14.

Behaviour of the failure paths is otherwise correct: staging is removed; T-INS-06 restores the previous binary. The only missing piece is a **single integer** exit code from the function and a clean exit of the process.

## Reviewer Briefing
- HIGH: Every `return <n>` path in `Invoke-DrawplateInstall` must yield exactly one `[int]`; nothing else on the output stream.
- HIGH: T-INS-01/03/04/05/06/12 must pass on **both** Windows shells with the documented codes.
- MEDIUM: Prefer fixing output pollution (`Write-Ui` → error/info streams only, or `| Out-Null` on side effects) over discarding arrays at the cast site.
- MEDIUM: Do not change main CI FFmpeg hybrid setup; it is green.
- LOW: Dist generator already green; only regenerate if `install.ps1` source changes.

## [ ] Step 1 — Make `Invoke-DrawplateInstall` return a pure int
- Files: `scripts/src/ps1/install.ps1` (then regenerate dist via `scripts/build-installers.ps1`); optionally tighten assertions in `scripts/tests/ps1/install.Tests.ps1` if diagnostics are noisy.
- Symbols: `Invoke-DrawplateInstall`, `Write-Ui`, final block `$exitCode = [int](Invoke-DrawplateInstall)` / `[Environment]::Exit`.
- Current → Desired: function output stream is a single integer 0–16; process exit code equals that integer when launched with `-File`.
- Change:
  1. Audit every write inside `Invoke-DrawplateInstall`. UI lines must not land on the success pipeline (use `[Console]::Out` only already preferred; ensure no bare strings or cmdlets emit to output).
  2. Ensure every path ends with `return <int>` only (no accidental second return value).
  3. Keep `$PSCommandPath` / `$MyInvocation.MyCommand.Path` detection and `[Environment]::Exit($exitCode)` for `-File` runs; `$global:LASTEXITCODE` for piped/`iex`.
  4. Regenerate `scripts/dist/install.ps1` and commit if different.
- Depends on: none.
- Edge cases: success path after PATH update and FFmpeg offer (T-INS-01/12 log showed full success UI then cast failure); failure paths 12/13/14.
- Tests: existing T-INS-*; do not weaken expectations.
- Verify:
  ```
  # Installer CI: windows-pwsh-7 and windows-powershell-51 all T-INS Passed
  # generated-up-to-date, shell-ubuntu, shell-macos remain success
  ```
- Done when: Installer CI fully green (all five jobs).
- Risk: MEDIUM
- Confidence: HIGH (cause is explicit in the log)

## [ ] Step 2 — Dist current + R18 sign-off
- Files: `scripts/dist/*` only if Step 1 changed sources.
- Verify: `pwsh -NoProfile -File scripts/build-installers.ps1` then `git diff --exit-code scripts/dist`.
- Done when: tip has green Installer CI; PROGRESS.md marks R18 DONE with run link.
- Risk: LOW
- Confidence: HIGH

## Global Definition of Done
- Installer CI all jobs green (including both Windows shells, all T-INS).
- Main CI remains green (hybrid FFmpeg unchanged).
- `scripts/dist/` matches sources.
- PROGRESS.md: R18 DONE with [V] run id.
- One commit per step; stop for Job B. No R19.

## Execution notes
- Scope = pure exit-code / output-stream fix in the PowerShell installer + tests if needed.
- Do not reintroduce apt on Ubuntu. Do not use setup-ffmpeg on macOS ARM64.
- If after fixing the Object[] issue a *different* wrong code appears, STOP and report with the new log line.


## Restored repair stages R19–R20

Source: the original roadmap in commit [87b50c2](https://github.com/migueljocode/Drawplate/blob/87b50c2a9063c5deb2cc5465a88617664e38af86/plan.md), which introduced repair stages R01–R20. These stage definitions were accidentally omitted by the focused R18 rewrite. They are restored here without changing their original scope.

### R19 Documentation, release engineering, honest status

- **Objective:** documents match reality; a tag produces verified artifacts. **Evidence:** B26, B27. Specification: sections H and I.6; old tasks T10.1 to T10.7.
- **Files:** `README.md`, `docs/{FFMPEG,HARDWARE,MIGRATION,PROVENANCE,VNEAT-BASELINE}.md`, `docs/decisions/*`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `.github/workflows/release.yml`, `.github/dependabot.yml`, `PROGRESS.md`, `tests/Drawplate.Tests/Cli/ReadmeTests.cs`.
- **Tasks:**
  1. README per H.1 including the 13 "Troubleshooting FFmpeg" entries of H.2 and an options table checked against `OptionTable` by `ReadmeTests`; remove the reference to "section H.5".
  2. `VNEAT-BASELINE.md` = the verified behavior notes of the research report (sections 5.2 to 5.4 and 6); if the Project doc is unreachable, STOP and ask the owner to paste them.
  3. ADRs the old tasks name and that are missing (0002 name, 0004 and later from the T3 spikes): each records the spike actually performed; a spike that was not performed is recorded as [U].
  4. `release.yml` (old T10.1), `dependabot.yml`, pinned action SHAs, CHANGELOG limited to what exists.
  5. `PROGRESS.md`: rewrite so every claim has [V]/[I]/[U] and links to the run or command; list R01 to R20 with results; no APPROVED.
- **Acceptance:** `ReadmeTests` pass; a dry-run tag on a fork produces archives, `SHA256SUMS` and attestation; no document describes an unbuilt feature.
- **Tests:** `ReadmeTests`, release workflow run. **Depends:** R18.

### R20 Acceptance on the target machine (owner, manual)

- **Objective:** confirm the hypotheses H1 to H3 and sign off. **Machine:** Ubuntu 26.04, i5-5200U, GeForce 820M, VAAPI working.
- **Evidence:** the target machine has never been run by the auditor; every hardware statement in this plan is [I] or [U] until this stage (Part 3, hypotheses H1 to H3).
- **Tests:** manual, by the owner; the commands below are the test.
- **Tasks (owner runs, pastes output into `docs/HARDWARE.md`; mark each result [V]):**
  1. `ffmpeg -version | head -1`; `vainfo | grep -i -E 'H264.*Enc'`.
  2. `drawplate -h`, `drawplate -V`, `drawplate -l` (expected: `vaapi` and `cpu`, no `nvenc`; `qsv` only if it truly works), `drawplate doctor`.
  3. `drawplate --verbose -q 50 <short clip>`: the printed argv must contain `-vaapi_device /dev/dri/renderD…` and `format=nv12,hwupload`; the output plays and is H.264/AAC.
  4. `drawplate -c <clip>` (no hardware probe, no note), `drawplate -e nvenc <clip>` (E-RUN-02, exit 1), Ctrl-C during a conversion (exit 130 within 5 s, no `.partial`).
  5. `drawplate` in a folder with 2 clips, and `drawplate --ffmpeg-path /nope -l` (E-FF-02, exit 3).
  6. If `-vaapi_device` is rejected by FFmpeg 8 (H3), STOP: the owner approves a new stage adding the `-init_hw_device` variant.
- **Acceptance:** every step behaves as written; results committed to `docs/HARDWARE.md`; the owner adds R20 to the "Approved stages" line under table S.
- **Depends:** R19.
