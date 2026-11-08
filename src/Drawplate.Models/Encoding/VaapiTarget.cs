namespace Drawplate.Models.Encoding;

/// <summary>A VAAPI render node (for example /dev/dri/renderD128) and whether to request low-power encoding.</summary>
public sealed record VaapiTarget(string Device, bool LowPower);
