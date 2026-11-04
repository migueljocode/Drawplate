# Drawplate vneat baseline

This document records the facts measured on Bash `vneat` 2.0.0, Linux, ffmpeg 6.1.1 (31 Oct 2026), and the Drawplate behavior contract drawn from plan.md section B and matrix D.3.

## Measured facts (section B)

- Quality q maps to CRF `51 - floor(q/2)`: q 100, 99, 75, 50, 25, 1 give CRF 1, 2, 14, 26, 39, 51 (read from the `crf=` tag x264 writes into the output).
- `-s sd` on 1280x720 gives 852x480; on 720x1280 gives 480x852; 160x120 is never upscaled; 321x241 becomes 320x240.
- A file with a supported extension but no video stream fails inside ffmpeg with `Error opening output files: Invalid argument`.
- Two inputs that map to the same output name (`x/clip.mp4`, `y/clip.mov`) silently skipped the second one in vneat. Drawplate must refuse instead (F06).
- SIGTERM sent only to the vneat process was ignored while ffmpeg ran. Drawplate must stop within 5 seconds (F23).
- `ffmpeg -hide_banner -i <file>` prints `Duration: HH:MM:SS.xx` and `Stream #0:N...: Video: ...` lines to stderr.
- `-progress pipe:1` prints `key=value` lines; `out_time_us` and `out_time_ms` both hold microseconds; a block ends with `progress=continue` or `progress=end`.
- winget package `Gyan.FFmpeg` 9.0.2: portable zip (no installer), command aliases `ffmpeg`, `ffplay`, `ffprobe`; the manifest SHA-256 matched the real download; the zip is 245.8 MiB and each exe is about 217 MiB unpacked; `ffmpeg.exe` contains libx264, h264_nvenc, h264_amf, h264_qsv (string search, not executed).
- Homebrew's `ffmpeg` formula builds with `--enable-gpl`, `--enable-libx264` and VideoToolbox.

## Behavior contract

Drawplate behavior is specified in plan.md sections D (behavior), E (message catalog), and matrix D.3 (feature matrix F01-F31). Section D.3 lists each row with its acceptance criteria and tests.
