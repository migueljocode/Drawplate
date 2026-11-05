using System;
using System.IO;

using Drawplate.Models.Cli;
using Drawplate.Models.Planning;

namespace Drawplate.Services.Conversion;

public static class InputDiscovery
{
    public static (ImmutableArray<string> Files, Message? Error) FromFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (!Directory.Exists(folder))
        {
            return ([], Messages.E_USAGE_07(folder));
        }

        var files = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in Directory.GetFileSystemEntries(folder))
        {
            var name = Path.GetFileName(entry);
            if (name is null)
            {
                continue;
            }
            if (InputRules.IsHidden(name))
            {
                continue;
            }
            if (!InputRules.IsSupported(name))
            {
                continue;
            }
            files.Add(name);
        }

        Array.Sort(files.ToArray(), InputRules.NameOrder);
        return (files.ToImmutable(), null);
    }
}
