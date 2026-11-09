using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Drawplate.Services.Platform;

/// <summary>Finds VAAPI render nodes (/dev/dri/renderD*), sorted so the first node is always the same.</summary>
public static class VaapiDevices
{
    private const string DriFolder = "/dev/dri";

    public static ImmutableArray<string> Find(Func<string, IEnumerable<string>> listDirectory)
    {
        ArgumentNullException.ThrowIfNull(listDirectory);
        return [.. listDirectory(DriFolder)
            .Where(path => Path.GetFileName(path).StartsWith("renderD", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Entries of <paramref name="folder"/> that look like render nodes; empty when it is missing or unreadable.</summary>
    public static IEnumerable<string> ListDirectory(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            return Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder, "renderD*") : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
