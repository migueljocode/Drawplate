using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Drawplate.Services.Platform;

/// <summary>PATH parsing in one place: empty entries are dropped and relative entries ignored (they would resolve against the current folder).</summary>
public static class PathFolders
{
    public static ImmutableArray<string> Parse(string? pathVariable) =>
        pathVariable is null
            ? []
            : [.. pathVariable
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Path.IsPathRooted)];
}
