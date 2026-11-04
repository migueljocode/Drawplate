using System;

namespace Drawplate.Models.Planning;

public static class InputRules
{
    public static bool IsSupported(string fileName)
    {
        if (fileName is null)
        {
            return false;
        }

        var dot = fileName.LastIndexOf('.');
        if (dot <= 0 || dot == fileName.Length - 1)
        {
            return false;
        }

        var ext = fileName[(dot + 1)..];
        return string.Equals(ext, "mov", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, "mp4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, "mkv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, "avi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, "webm", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHidden(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.StartsWith('.');
    }

    public static int NameOrder(string x, string y)
    {
        var ci = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        if (ci != 0)
        {
            return ci;
        }
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}
