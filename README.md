# Drawplate

A cross-platform command-line tool (Native-AOT .NET) that converts batches of videos to H.264/AAC MP4 with FFmpeg. It replaces the `vneat` scripts (`Bash/vneat`, `Zsh/vneat.zsh`, `PowerShell/vneat.ps1`) in `migueljocode/WorkWithIO`.

## What it is

Drawplate converts videos (mov, mp4, mkv, avi, webm) from a folder or an explicit file list into `.mp4` (H.264 video, AAC 96k audio, `+faststart`) in an output folder, with a quality percentage, an optional resolution cap, hardware encoders when they work and the CPU (libx264) otherwise, live progress, and a summary.

## Install

Drawplate installs into user space and never needs elevation:

```sh
# Linux and macOS
curl -fsSL https://raw.githubusercontent.com/migueljocode/Drawplate/main/scripts/dist/install.sh | sh

# Windows
irm https://raw.githubusercontent.com/migueljocode/Drawplate/main/scripts/dist/install.ps1 | iex
```

After a release is tagged, prefer the tag-pinned one-liners from the release notes so the scripts match the binary.

## Quick start

```sh
# Convert the current folder
drawplate

# Convert a folder and its files
drawplate -i ~/Videos -o ~/Videos/compressed -q 75

# Convert specific files
drawplate clip1.mov clip2.avi -q 90

# CPU only
drawplate -c -q 80
```

## Options

| Option | Meaning |
|--------|---------|
| `-i`, `--input <folder>` | Input folder (default: current directory) |
| `-o`, `--output <folder>` | Output folder (default: `<input>/compressed` or `./compressed`) |
| `-q`, `--quality <1-100>` | Quality, higher is better (default: 50) |
| `-s`, `--max-size <size>` | Cap: `sd`, `hd`, `fullhd`, or `4k` |
| `-e`, `--encoder <name>` | `auto`, `nvenc`, `qsv`, `vaapi`, `amf`, `videotoolbox`, or `cpu` |
| `-c`, `--cpu` | Force CPU (libx264); same as `-e cpu` |
| `-l`, `--list-encoders` | Print encoders that work on this machine, then exit |
| `-m`, `--interactive` | Interactive menu (needs a terminal) |
| `-t`, `--no-timestamps` | Do not copy original file dates |
| `-y`, `--install-deps` | Run the FFmpeg install command without asking, then exit |
| `-n`, `--no-install` | Never offer or run an install |
| `-h`, `--help` | Show help |
| `-V`, `--version` | Print version |
| `--ffmpeg-path <file>` | Use this FFmpeg executable |
| `--verbose` | Print ffmpeg arguments and stderr tails |

Run `drawplate --help` for the same list generated from the source.

## FFmpeg

Drawplate does not download or bundle FFmpeg. It finds FFmpeg on your machine (`--ffmpeg-path`, `DRAWPLATE_FFMPEG`, PATH, known folders). Use `drawplate setup` or `drawplate doctor` to get it installed. See [docs/FFMPEG.md](docs/FFMPEG.md) for discovery order and validation rules.

### Troubleshooting FFmpeg

| # | Symptom | What to do |
|---|---------|------------|
| 1 | FFmpeg not found | Run `drawplate setup` (or `drawplate -y`). On Windows the first suggestion is `winget install --id Gyan.FFmpeg -e` (~246 MiB download). On macOS: `brew install ffmpeg`. On Linux the package-manager command is printed only (needs sudo). |
| 2 | winget missing or blocked | Install winget, or use Scoop / Chocolatey, or pass `--ffmpeg-path`, or set `DRAWPLATE_FFMPEG`. |
| 3 | winget succeeded but Drawplate still cannot find FFmpeg | Open a **new** terminal so PATH refreshes, or rely on Drawplate’s check of the WinGet Links folder. |
| 4 | `--ffmpeg-path` or `DRAWPLATE_FFMPEG` points at a bad file (E-FF-02) | The path is authoritative; Drawplate does not fall through to PATH. Fix the path or clear the variable. |
| 5 | FFmpeg older than 6.0 (E-FF-07) | Upgrade: `winget upgrade --id Gyan.FFmpeg -e`, `brew upgrade ffmpeg`, or your package manager. |
| 6 | Build has no libx264 and no working hardware encoder (E-FF-03) | Install a full build (Gyan on Windows, Homebrew on macOS, distro package with libx264 on Linux). |
| 7 | Homebrew not installed (macOS) | Install from https://brew.sh, then `brew install ffmpeg`. |
| 8 | Linux without sudo | Use a static build you place on PATH, or set `--ffmpeg-path` / `DRAWPLATE_FFMPEG`. Drawplate never runs sudo. |
| 9 | GPU present but only CPU encodes | Expected when drivers or encoder support are missing. Drawplate prints one neutral note and continues on CPU. Drivers are the user’s job. |
| 10 | NVENC / QSV / AMF / VAAPI fails for one file | Drawplate falls back to the next encoder and finally CPU. Use `-l` / `doctor` to see what works. |
| 11 | Corporate proxy or offline machine | Install FFmpeg offline, then point Drawplate at it with `--ffmpeg-path`. |
| 12 | `doctor` reports missing / broken / outdated | Follow the line’s next step; notes never change the exit code. Exit 3 means FFmpeg is unusable. |
| 13 | Want more detail | Run with `--verbose` to see exact argv and stderr tails. |

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success (skips allowed) |
| 1 | A file failed, nothing to convert, or an unexpected error |
| 2 | Usage or validation error |
| 3 | FFmpeg unavailable and not installed |
| 130 | Interrupted (Ctrl-C / SIGTERM) |

## License

MIT. FFmpeg is not distributed with Drawplate and is licensed under its own terms. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
