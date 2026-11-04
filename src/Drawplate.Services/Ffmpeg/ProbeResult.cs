using Drawplate.Models.Encoding;

namespace Drawplate.Services.Ffmpeg;

public enum ProbeFailureKind
{
    InvalidInput,
    EncoderUnavailable,
    NvencDriverTooOld,
    DeviceAccess,
    Unknown
}

public sealed record ProbeResult(
    EncoderKind Kind,
    bool Passed,
    ProbeFailureKind? FailureKind,
    string StderrTail);
