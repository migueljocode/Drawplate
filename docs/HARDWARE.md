# Hardware acceleration results

Results of T6.7 (manual hardware checklist). Fill in per vendor as hardware is tested.

| Vendor | OS | GPU | Driver | FFmpeg | `drawplate -l` | Result |
|--------|----|-----|--------|--------|----------------|--------|
| NVIDIA | Ubuntu 26.04 LTS | GeForce 820M (discrete) | no NVENC path observed in FFmpeg | FFmpeg 8.0.1 | `vaapi`, `cpu` | [V] `drawplate -e nvenc` failed with exit 1 (`Encoder 'nvenc' is not working on this machine.`) |
| Intel | Ubuntu 26.04 LTS | Core i5-5200U iGPU / Intel Gen Graphics | Intel iHD driver for Intel(R) Gen Graphics - 26.1.2 | FFmpeg 8.0.1 | `vaapi`, `cpu` | [V] VAAPI accepted; conversion succeeded with `-vaapi_device /dev/dri/renderD128` and `format=nv12,hwupload` |
| AMD | | | | | | |
| Apple | | | | | | |

Mark each section [V] when verified on real hardware. Drawplate never installs GPU drivers; they are the user's job.

## Hardware note

When a GPU is detected but no hardware encoder passes, Drawplate prints a neutral informational note (N-GPU-01 / N-GPU-02). It never changes the exit code and is never a warning.

## 2026-11-11 — R20 acceptance evidence [V]

Environment checks:

```bash
$ ffmpeg -version | head -1
ffmpeg version 8.0.1-3ubuntu2 Copyright (c) 2000-2025 the FFmpeg developers

$ vainfo 2>&1 | grep -i -E 'H264|VAProfile|Driver' || true
libva info: Found init function __vaDriverInit_1_22
libva info: va_openDriver() returns 0
vainfo: Driver version: Intel iHD driver for Intel(R) Gen Graphics - 26.1.2 ()
      VAProfileH264Main               :  VAEntrypointVLD
      VAProfileH264Main               :  VAEntrypointEncSlice
      VAProfileH264High               :  VAEntrypointVLD
      VAProfileH264High               :  VAEntrypointEncSlice
      VAProfileH264ConstrainedBaseline:  VAEntrypointVLD
      VAProfileH264ConstrainedBaseline:  VAEntrypointEncSlice

$ ls -l /dev/dri/ 2>/dev/null || true
total 0
drwxr-xr-x  2 root root        100 Sep 16 18:25 by-path
crw-rw----+ 1 root video  226,   1 Nov 11 22:18 card1
crw-rw----+ 1 root render 226, 128 Nov 11 22:18 renderD128

$ drawplate -l
vaapi
cpu

$ drawplate doctor
ok        FFmpeg      /usr/bin/ffmpeg (8.0.1, found on PATH)
ok        libx264     present
ok        ffprobe     /usr/bin/ffprobe (optional)
ok        Encoders    vaapi, cpu
```

VAAPI conversion acceptance (real output):

```bash
$ CLIP=/tmp/drawplate-r20.mp4
$ drawplate --verbose -q 50 -o /tmp/drawplate-r20-out "$CLIP"
probe vaapi: -nostdin -hide_banner -loglevel quiet -vaapi_device /dev/dri/renderD128 -f lavfi -i color=c=black:s=256x256:d=0.1 -vf scale=trunc(iw/2)*2:trunc(ih/2)*2,format=nv12,hwupload -c:v h264_vaapi -global_quality 26 -frames:v 1 -f null -
Plan
Encoder   [VAAPI] > [CPU]
ffmpeg -nostdin -hide_banner -loglevel error -nostats -progress pipe:1 -vaapi_device /dev/dri/renderD128 -i /tmp/drawplate-r20.mp4 -map 0:v:0 -map 0:a:0? -sn -dn -vf scale=trunc(iw/2)*2:trunc(ih/2)*2,format=nv12,hwupload -c:v h264_vaapi -global_quality 26 -c:a aac -b:a 96k -movflags +faststart -f mp4 -y /tmp/drawplate-r20-out/.drawplate-r20.28619.partial.mp4
Summary
Result    1 converted
Output    /tmp/drawplate-r20-out
All done.

$ ffprobe -v error -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 /tmp/drawplate-r20-out/*.mp4
h264
aac
```

CPU and failure checks:

```bash
$ drawplate -c -q 50 -o /tmp/drawplate-r20-cpu /tmp/drawplate-r20.mp4
Plan
Encoder   [CPU]
Summary
Result    1 converted
exit:0

$ drawplate -e nvenc -o /tmp/drawplate-r20-nvenc /tmp/drawplate-r20.mp4
Encoder 'nvenc' is not working on this machine.
  Run 'drawplate --list-encoders' to see what works.
exit:1

$ python3 - <<'PY'
# SIGINT during a long encode; returns 130 and no .partial files remain
PY
pid 30423
probe nvenc: -nostdin -hide_banner -loglevel quiet -f lavfi -i color=c=black:s=256x256:d=0.1 -vf scale=trunc(iw/2)*2:trunc(ih/2)*2 -c:v h264_nvenc -rc vbr -cq 26 -b:v 0 -pix_fmt yuv420p -frames:v 1 -f null -
Interrupted.

exit 130
partial files: []
```

Folder mode and missing FFmpeg path:

```bash
$ drawplate -i /tmp/drawplate-r20-folder -o /tmp/drawplate-r20-folder-out
Result    2 converted
exit:0

$ drawplate --ffmpeg-path /nope -l
FFmpeg at /nope could not be run (file not found).
  Reinstall it, or choose another with --ffmpeg-path.
exit:3
```

Conclusion: [V] H1 passes on this target machine (`vaapi` is available and used successfully). [V] H2 holds for this GPU (`nvenc` is not accepted). [V] H3 passes: FFmpeg accepts `-vaapi_device /dev/dri/renderD128` and the VAAPI path remains valid on this machine.
