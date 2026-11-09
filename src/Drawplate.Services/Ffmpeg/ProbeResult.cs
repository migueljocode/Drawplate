using Drawplate.Models.Encoding;

namespace Drawplate.Services.Ffmpeg;

public enum ProbeFailureKind
{
    InvalidInput,
    EncoderUnavailable,
    NvencDriverTooOld,
    DeviceAccess,
    /// <summary>FFmpeg does not list the encoder, so no probe was run.</summary>
    NotListed,
    /// <summary>VAAPI was requested but no /dev/dri/renderD* node exists, so no probe was run.</summary>
    NoDevice,
    Unknown
}

/// <param name="Vaapi">For a passing VAAPI probe: the render node and mode that worked.</param>
public sealed record ProbeResult(
    EncoderKind Kind,
    bool Passed,
    ProbeFailureKind? FailureKind,
    string StderrTail,
    VaapiTarget? Vaapi = null);
