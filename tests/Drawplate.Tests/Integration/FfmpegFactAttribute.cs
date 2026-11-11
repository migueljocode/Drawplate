using System;

using Xunit;

namespace Drawplate.Tests.Integration;

public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (MediaFixtures.FindFfmpeg() is null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "FFmpeg is not installed; install FFmpeg to run integration tests.";
    }
}
