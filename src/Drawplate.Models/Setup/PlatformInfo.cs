using System.Collections.Immutable;

using Drawplate.Models.Encoding;

namespace Drawplate.Models.Setup;

public sealed record PlatformInfo(
    OperatingSystemKind Os,
    string Architecture,
    ImmutableHashSet<string> Tools,
    string? LinuxId,
    string? LinuxIdLike,
    int? FedoraRelease);

public sealed record InstallSuggestion(
    string Title,
    ImmutableArray<string> DisplayLines,
    string? Executable,
    ImmutableArray<string> Arguments,
    bool RunnableByApp);
