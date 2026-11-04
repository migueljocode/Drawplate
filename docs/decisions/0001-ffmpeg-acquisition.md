# ADR 0001: FFmpeg acquisition

## Status

Accepted

## Context

Drawplate converts video with FFmpeg but must not download, bundle, or host FFmpeg itself in v1 (section B, out of scope). The user installs FFmpeg; Drawplate locates it and offers an install command. winget on Windows and Homebrew on macOS are runnable without elevation; Linux package managers require administrator rights and are only printed.

## Decision

- Locate FFmpeg in this order: `--ffmpeg-path`, `DRAWPLATE_FFMPEG`, PATH, known folders (section D.5).
- winget is the first suggestion on Windows; Homebrew first on macOS; Linux suggestions are printed only, never run (section D.5).
- Drawplate never downloads FFmpeg itself and never calls `sudo`, `pkexec`, or `runas`.
- `drawplate setup` offers to run the install command; `-y` runs it without asking and exits; `-n` never offers.
- `doctor` reports FFmpeg, encoders, and hardware read-only (section D.6).

## Alternatives

- Bundling FFmpeg: rejected (out of scope v1, licensing, size).
- Downloading FFmpeg at runtime: rejected (security, network, no elevation).
- Always requiring manual PATH setup: rejected (poor UX; winget/Homebrew offer a smoother path).

## Consequences

- v1 does not manage GPU drivers; they are the user's job.
- Linux users run the package manager command themselves.
- The installer scripts (P8) install Drawplate itself, not FFmpeg, and offer `drawplate setup` at the end.
