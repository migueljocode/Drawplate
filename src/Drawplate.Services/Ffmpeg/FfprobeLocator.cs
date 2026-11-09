using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;
using Drawplate.Services.Platform;

namespace Drawplate.Services.Ffmpeg;

/// <summary>ffprobe is optional (D.5): next to FFmpeg first, then on PATH, otherwise null.</summary>
public static class FfprobeLocator
{
    public static string? Find(
        string ffmpegPath,
        IEnumerable<string> pathFolders,
        Func<string, bool> fileExists,
        OperatingSystemKind? os = null)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);
        ArgumentNullException.ThrowIfNull(pathFolders);
        ArgumentNullException.ThrowIfNull(fileExists);

        var name = (os ?? PlatformDetector.DetectOs()) == OperatingSystemKind.Windows ? "ffprobe.exe" : "ffprobe";

        if (Path.GetDirectoryName(ffmpegPath) is { Length: > 0 } sibling)
        {
            var candidate = Path.Combine(sibling, name);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return pathFolders
            .Where(Path.IsPathRooted)
            .Select(folder => Path.Combine(folder, name))
            .FirstOrDefault(fileExists);
    }
}
