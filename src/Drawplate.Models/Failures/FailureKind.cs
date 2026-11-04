namespace Drawplate.Models.Failures;

public enum FailureKind
{
    InvalidInput,
    NoVideoStream,
    EncoderUnavailable,
    NvencDriverTooOld,
    DeviceAccess,
    Unknown
}
