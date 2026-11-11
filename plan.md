# Plan — R20 Acceptance on the target machine

**Status:** R01–R19 **DONE** on `main` @ `de0bb21`. Actor/owner executes **only** R20. Do not change product `src/`, installers, or CI unless a command in this plan fails for a code reason (then STOP and report).

## Context

| Item | Value |
|------|--------|
| Machine | Ubuntu 26.04 LTS, Intel Core i5-5200U (iGPU), NVIDIA GeForce 820M (no NVENC) |
| Expected | VAAPI works (owner statement); NVENC should not |
| FFmpeg | Distro or user-installed; minimum 6.0 |
| Evidence file | `docs/HARDWARE.md` (fill in; mark each result `[V]`) |
| Status file | `PROGRESS.md` (mark R20 DONE when acceptance passes) |

Hypotheses from the audit (still `[U]` until this stage):

- **H1** — iGPU encodes through the standard VAAPI entry point.
- **H2** — `h264_qsv` fails on this GPU.
- **H3** — FFmpeg still accepts `-vaapi_device <node>` the way vneat used it (if rejected, STOP for a follow-up stage with `-init_hw_device`).

## Reviewer Briefing

- HIGH: Every command output that is evidence must be pasted into `docs/HARDWARE.md` (or attached as fenced blocks). Do not invent results.
- HIGH: Do not “fix” encoder profiles or argv on this branch unless H3 fails and the owner explicitly approves a new stage.
- MEDIUM: Use a short real clip (a few seconds). Prefer a file already on the machine; otherwise generate one with lavfi.
- LOW: If `drawplate` is not on PATH, install with the user-space installer from `main` or point at a local publish output.

## Prerequisites

```bash
# Binary available
command -v drawplate || echo "install drawplate first (scripts/dist/install.sh or local publish)"
drawplate -V

# FFmpeg available
ffmpeg -version | head -3
command -v vainfo && vainfo 2>&1 | head -40 || echo "vainfo not installed (optional but useful)"
```

Generate a test clip if needed:

```bash
ffmpeg -y -loglevel error -f lavfi -i testsrc2=size=640x360:rate=30:duration=2 \
  -f lavfi -i sine=frequency=440:duration=2 -c:v libx264 -c:a aac -shortest /tmp/drawplate-r20.mp4
CLIP=/tmp/drawplate-r20.mp4
```

## [ ] Step 1 — Environment and doctor

- Commands:
  ```bash
  ffmpeg -version | head -1
  vainfo 2>&1 | grep -i -E 'H264|VAProfile|Driver' || true
  ls -l /dev/dri/ 2>/dev/null || true
  drawplate -h | head -40
  drawplate -V
  drawplate -l
  drawplate doctor
  ```
- Record in `docs/HARDWARE.md`: FFmpeg version, VAAPI / driver lines, `drawplate -l` output, full `doctor` output.
- Expect: `vaapi` and `cpu` listed; `nvenc` absent or failing; `qsv` only if it truly works (H2).

## [ ] Step 2 — VAAPI conversion (verbose)

- Command:
  ```bash
  drawplate --verbose -q 50 -o /tmp/drawplate-r20-out "$CLIP"
  ```
- Acceptance:
  - Exit 0 (or 1 only if a non-VAAPI issue is explained).
  - Verbose argv contains `-vaapi_device /dev/dri/renderD…` and filter piece `format=nv12,hwupload` when VAAPI is selected.
  - Output file plays and is H.264 + AAC (e.g. `ffprobe` or `ffmpeg -i` on the output).
- If FFmpeg rejects `-vaapi_device` (H3 fails): **STOP**. Do not invent a new device API. Report the exact stderr; owner may approve a follow-up stage for `-init_hw_device`.

## [ ] Step 3 — CPU force, bad encoder, cancellation

- Commands:
  ```bash
  drawplate -c -q 50 -o /tmp/drawplate-r20-cpu "$CLIP"          # no hardware probe required; no GPU note required
  drawplate -e nvenc -o /tmp/drawplate-r20-nvenc "$CLIP" ; echo exit:$?   # expect E-RUN-02, exit 1
  # Ctrl-C during a longer encode, or:
  # start encode in background and kill -INT the drawplate PID — expect exit 130 within 5 s, no .partial left
  ```
- Record exit codes and whether any `.partial` remains.

## [ ] Step 4 — Folder mode and missing FFmpeg path

- Commands:
  ```bash
  mkdir -p /tmp/drawplate-r20-folder
  cp "$CLIP" /tmp/drawplate-r20-folder/a.mp4
  cp "$CLIP" /tmp/drawplate-r20-folder/b.mp4
  drawplate -i /tmp/drawplate-r20-folder -o /tmp/drawplate-r20-folder-out
  drawplate --ffmpeg-path /nope -l ; echo exit:$?   # expect E-FF-02, exit 3
  ```
- Acceptance: both clips convert (or skip with clear reason on re-run); bad path exits 3 with E-FF-02.

## [ ] Step 5 — Write evidence and close R20

- Files: `docs/HARDWARE.md`, `PROGRESS.md`
- Update `docs/HARDWARE.md`:
  - Fill the vendor table row(s) that apply (Intel / NVIDIA as observed).
  - Add a dated section with command transcripts (trimmed is fine; keep argv lines and exit codes).
  - Mark each verified claim `[V]`.
- Update `PROGRESS.md`:
  - R20 Result: **DONE** with date and a one-line summary (VAAPI yes/no, note behaviour, any H3 issue).
  - Move hardware unknowns out of “Unverified” when resolved.
- Commit message style: `docs(hardware): R20 acceptance on target machine`.
- Do **not** start new feature work.

## Global Definition of Done

- All steps above executed on the real target machine (not CI).
- `docs/HARDWARE.md` contains real output marked `[V]`.
- `PROGRESS.md` shows R20 **DONE**.
- Main CI remains green (docs-only commit).
- If H3 fails: R20 stays open or is closed as “blocked on device API follow-up” with the stderr quoted — do not guess argv.

## Out of scope

- Changing encoder profiles, argv builders, or probes (unless owner opens a new stage after H3 failure).
- Installing GPU drivers (user’s job).
- Release tagging (optional after R20).
- Dependabot PRs.

## Execution notes

- Prefer a single docs commit (HARDWARE + PROGRESS).
- Sanitize paths if the owner does not want home-directory layout public; keep device nodes and argv flags.
- Agent without access to the target machine: prepare the `docs/HARDWARE.md` template sections and the exact command block for the owner, then STOP for owner paste-back.
