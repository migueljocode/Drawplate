# FFmpeg and Drawplate

Drawplate does not download, bundle, or host FFmpeg. You install FFmpeg yourself; Drawplate locates it.

## How Drawplate finds FFmpeg

In this order:
1. `--ffmpeg-path <file>`
2. `DRAWPLATE_FFMPEG` environment variable
3. `PATH`
4. Known folders:
   - Windows: `%LOCALAPPDATA%\Microsoft\WinGet\Links`, `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_*\ffmpeg-*\bin`, `%USERPROFILE%\scoop\shims`, `%ProgramData%\chocolatey\bin`
   - macOS/Linux: `/opt/homebrew/bin`, `/usr/local/bin`, `/home/linuxbrew/.linuxbrew/bin`, `/usr/bin`

`--ffmpeg-path` and `DRAWPLATE_FFMPEG` are authoritative: if they point at something unusable, Drawplate reports `E-FF-02` and does not fall through.

## Validation

The candidate must:
- Run (`ffmpeg -version` exits 0 within 10 s)
- Report version 6.0 or newer (master builds `N-<number>-g<hash>` count as new enough)
- Have parseable `-encoders` output

`libx264` absence is handled per F28/F03.

## ffprobe

Used when found next to FFmpeg or on PATH; otherwise duration and stream presence come from `ffmpeg -hide_banner -i <file>`. ffprobe is never required.

## Install suggestions

- Windows: `winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements` (portable, no administrator rights; ~245.8 MiB download, ~650 MB on disk). Also `scoop install ffmpeg` / `choco install ffmpeg` (choco needs Administrator).
- macOS: `brew install ffmpeg` (Homebrew builds with `--enable-gpl --enable-libx264` and VideoToolbox).
- Linux: `sudo apt install ffmpeg` (apt), `sudo pacman -S ffmpeg` (pacman), or the RPM Fusion two-step (Fedora). Printed only, never run by Drawplate.

## License

FFmpeg is licensed under its own terms (GPL when built with libx264). Drawplate does not distribute FFmpeg.
