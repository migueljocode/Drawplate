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

Run `drawplate --help` for the full list. The option table is generated from the source.

## FFmpeg

Drawplate does not download or bundle FFmpeg. It finds FFmpeg on your machine (`--ffmpeg-path`, `DRAWPLATE_FFMPEG`, PATH, known folders). Use `drawplate setup` or `drawplate doctor` to get it installed.

## Exit codes

- 0: success (skips allowed)
- 1: a file failed, nothing to convert, or an unexpected error
- 2: usage or validation error
- 3: FFmpeg unavailable and not installed
- 130: interrupted

## License

MIT. FFmpeg is not distributed with Drawplate and is licensed under its own terms.
