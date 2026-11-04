using System;

namespace Drawplate.Models.Ffmpeg;

public sealed record FfmpegVersion(Version Version, bool IsMaster, string Raw);
