using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Drawplate.Tests")]

namespace Drawplate.Models.Cli;

internal enum ExitCode
{
    Ok = 0,
    Failure = 1,
    UsageError = 2,
    FfmpegUnavailable = 3,
    Interrupted = 130
}
