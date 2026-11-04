using System;

namespace Drawplate.Services.Ffmpeg;

public sealed record MediaInfo(TimeSpan? Duration, bool HasVideo);
