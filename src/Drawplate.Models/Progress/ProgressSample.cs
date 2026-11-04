using System;

namespace Drawplate.Models.Progress;

public readonly record struct ProgressSample(TimeSpan Elapsed, double? Speed, bool Ended);
