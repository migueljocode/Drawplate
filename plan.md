# Plan — R19 Documentation, honest status, release engineering

**Status:** R18 merged to `main` @ `c240e2c` (PR #17). Main CI green. Installer CI last green on pre-merge tip `dd33b21` (run 43059860922). Actor executes **only** R19. Do not start R20. Do not edit product `src/` or installer scripts unless a doc test requires a trivial fix.

## CI snapshot (verified 2026-11-11)

| Workflow | Result | Notes |
|----------|--------|-------|
| **CI** on main `c240e2c` [43062731366](https://github.com/migueljocode/Drawplate/actions/runs/43062731366) | **success** | Ubuntu, macOS, Windows green after R18 merge |
| **Installer CI** on R18 tip `dd33b21` [43059860922](https://github.com/migueljocode/Drawplate/actions/runs/43059860922) | **success** | All jobs including both Windows shells; T-INS exit codes fixed |

## R18 close-out (do this first)

PROGRESS.md on main still says R18 **IN PROGRESS**. Update it to **DONE** with the two run links above and a one-line note that the Object[] exit-code fix landed in the merge.

## Reviewer Briefing

- HIGH: No document may claim a feature that is not present in `src/` or that was never verified.
- HIGH: PROGRESS.md must use only [V]/[I]/[U] and must list R01–R20 with current results; remove every remaining “APPROVED” / “P0–P10 complete” claim that is not explicitly marked historical.
- MEDIUM: README options table must match `OptionTable` (covered by existing `ReadmeTests` if present; otherwise add a minimal check or document the gap as [U]).
- LOW: Release workflow and Dependabot can be scaffolded or left as [U] if the owner prefers to delay tagging.

## [ ] Step 1 — Mark R18 DONE in PROGRESS.md

- Files: `PROGRESS.md`
- Change: Replace the R18 “IN PROGRESS” block with a DONE entry that cites:
  - Main CI [43062731366](https://github.com/migueljocode/Drawplate/actions/runs/43062731366) on `c240e2c`
  - Installer CI [43059860922](https://github.com/migueljocode/Drawplate/actions/runs/43059860922) on `dd33b21`
  - One-sentence summary of the Object[] / exit-code fix.
- Done when: PROGRESS.md no longer claims R18 is open.
- Risk: LOW
- Confidence: HIGH

## [ ] Step 2 — Honest PROGRESS.md for the whole repair series

- Files: `PROGRESS.md`
- Change:
  1. Keep the historical “2026-11-05 All phases P0-P10 complete” line but keep the “superseded by repair stages R01–R20” note.
  2. Ensure R01–R16, R17, R18 each have a clear Result line with [V] evidence where available.
  3. Add R19 as **IN PROGRESS** (this plan) and R20 as **NOT STARTED** (owner manual).
  4. Remove any leftover “APPROVED” language that is not inside a historical-claim paragraph.
- Done when: a reader can tell at a glance which stages are closed and which are open.
- Risk: LOW
- Confidence: HIGH

## [ ] Step 3 — README and core docs match reality

- Files: `README.md`, `docs/FFMPEG.md`, `docs/MIGRATION.md`, `docs/PROVENANCE.md`, `docs/VNEAT-BASELINE.md`, `docs/HARDWARE.md` (leave the checklist empty for R20), any ADR under `docs/decisions/` that still claims unfinished work.
- Tasks:
  1. README: install one-liners point at the real `scripts/dist/` paths (or tag-pinned URLs once a release exists); options section does not invent flags; Troubleshooting FFmpeg has the 13 entries required by the original specification (or fewer if some were intentionally dropped—document the decision).
  2. Remove every remaining reference to “section H.5 of plan.md” or other internal plan anchors that a user cannot follow.
  3. `docs/HARDWARE.md` stays the empty template; do not invent results.
  4. Any ADR that describes a spike that was never run is marked [U].
- Acceptance: no document describes an unbuilt feature; no stub “TODO” left in user-facing docs.
- Tests: existing `ReadmeTests` (if any) must stay green; do not weaken them.
- Risk: MEDIUM (scope of “13 troubleshooting entries” may need owner clarification)
- Confidence: MEDIUM

## [ ] Step 4 — Release engineering scaffold (optional if time-boxed)

- Files: `.github/workflows/release.yml` (if missing or incomplete), `.github/dependabot.yml`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`.
- Tasks: make a dry-run-friendly release workflow that can produce the five RID archives + SHA256SUMS when a tag is pushed; pin action SHAs; keep CHANGELOG honest (only what exists).
- Acceptance: a fork dry-run or `workflow_dispatch` produces the expected artifact names; or the gap is recorded as [U] in PROGRESS.md and this step is deferred.
- Risk: MEDIUM
- Confidence: MEDIUM

## Global Definition of Done (R19)

- PROGRESS.md shows R18 DONE and R19 IN PROGRESS / DONE with evidence links.
- User-facing docs contain no false completion claims and no stubs that look finished.
- Main CI remains green.
- No product `src/` or installer behaviour change unless required by a doc test.
- One logical commit per step (or a single well-described commit if the owner prefers); stop for Job B review.
- Do **not** start R20 (owner hardware capture).

## R20 (not in this plan — owner only)

R20 remains the manual target-machine acceptance stage:

- Machine: Ubuntu 26.04, i5-5200U, GeForce 820M (VAAPI works per owner).
- Owner runs the commands listed in the restored R20 section (ffmpeg version, vainfo, `drawplate -l` / `doctor` / verbose conversion, CPU-only, forced NVENC rejection, Ctrl-C, missing ffmpeg-path) and pastes results into `docs/HARDWARE.md` marked [V].
- Actor may later open a tiny follow-up PR that only commits the filled HARDWARE.md and marks R20 DONE in PROGRESS.md.

## Execution notes

- Scope = documentation + status honesty + optional release scaffold.
- Prefer updating existing files over adding new ones.
- If a required “13 troubleshooting entries” list cannot be reconstructed from the research report or message catalogue, STOP and ask the owner.
- Do not touch `scripts/src` or `scripts/dist` unless a regenerated dist is needed for a README path fix (unlikely).
