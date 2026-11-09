using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Drawplate.Models.Cli;
using Drawplate.Models.Planning;

namespace Drawplate.Services.Conversion;

public static class InputDiscovery
{
    /// <summary>Supported, visible, regular files directly in <paramref name="folder"/>, as full paths, in <see cref="InputRules.NameOrder"/>.</summary>
    public static (ImmutableArray<string> Files, Message? Error) FromFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (!Directory.Exists(folder))
        {
            return ([], Messages.E_USAGE_07(folder));
        }

        var files = Directory.GetFileSystemEntries(folder)
            .Where(entry => File.Exists(entry))
            .Where(entry => !InputRules.IsHidden(Path.GetFileName(entry)) && InputRules.IsSupported(Path.GetFileName(entry)))
            .Select(entry => Path.GetFullPath(entry))
            .OrderBy(entry => Path.GetFileName(entry), Comparer<string>.Create(InputRules.NameOrder))
            .ToImmutableArray();

        return (files, null);
    }
}
