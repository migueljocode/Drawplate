# Migration guide from vneat

Drawplate replaces the `vneat` scripts (`Bash/vneat`, `Zsh/vneat.zsh`, `PowerShell/vneat.ps1`) from `migueljocode/WorkWithIO`.

## Option map (identical short flags)

| vneat | Drawplate | Meaning |
|-------|-----------|---------|
| `-i <folder>` | `-i <folder>`, `--input` | Input folder |
| `-o <folder>` | `-o <folder>`, `--output` | Output folder |
| `-q <n>` | `-q <n>`, `--quality` | Quality 1-100 |
| `-s <sd\|hd\|fullhd\|4k>` | `-s <...>`, `--max-size` | Resolution cap |
| `-e <enc>` | `-e <enc>`, `--encoder` | Encoder (auto, nvenc, qsv, vaapi, amf, videotoolbox, cpu) |
| `-c` | `-c`, `--cpu` | CPU only (libx264) |
| `-t` | `-t`, `--no-timestamps` | Do not copy timestamps |
| `-l` | `-l`, `--list-encoders` | List working encoders |
| `-h` | `-h`, `--help` | Help |
| `-V` | `-V`, `--version` | Version |
| `-y` | `-y`, `--install-deps` | Run FFmpeg install without asking |
| `-n` | `-n`, `--no-install` | Never offer install |
| `-m` | `-m`, `--interactive` | Interactive menu |

## What changed

- **Usage errors exit 2** (vneat varied).
- **Collisions are refused** (exit 2) instead of silently skipping the second file (F06).
- **`-y` runs winget (Windows) or Homebrew (macOS)**; Linux package manager commands are printed only (never run by Drawplate).
- **GPU drivers are never installed** by Drawplate; they are the user's job.
- **`VNEAT_NETWORK` / `VIDEO2MP4_NETWORK` dropped** (no internet pre-check).
- **Hidden files** (names starting with `.`) are skipped in folder mode (F26).
- **File order** is case-insensitive then ordinal (F25).
- **`drawplate setup`** shows how to get FFmpeg; `drawplate doctor` is a read-only check.
- **Partial files** use a unique PID-based name and are removed on failure/cancel (F04).
- **SIGTERM to the Drawplate PID** stops conversion within 5 s (F23) (vneat ignored it).
- **Audio-only files** fail with reason `no video stream` before ffmpeg runs (F14).
- **Output path that is a file** errors with E-USAGE-08, exit 2 (F03).
- **Exit codes**: 0 success, 1 file failure/nothing to convert, 2 usage error, 3 FFmpeg unavailable, 130 interrupted.

## FFmpeg

Drawplate does not download or bundle FFmpeg. Install it yourself:
- Windows: `winget install --id Gyan.FFmpeg -e`
- macOS: `brew install ffmpeg`
- Linux: your package manager (requires administrator rights)

Then run `drawplate doctor` to verify.
