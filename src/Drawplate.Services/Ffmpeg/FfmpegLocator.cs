using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;
using Drawplate.Services.Platform;

namespace Drawplate.Services.Ffmpeg;

public static class FfmpegLocator
{
    /// <summary>Child folders of <paramref name="folder"/>; empty when it is missing or cannot be read.</summary>
    public static IEnumerable<string> EnumerateDirectories(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            return Directory.GetDirectories(folder);
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

    public static FfmpegCandidate? Locate(
        string? ffmpegPath,
        string? envVar,
        IEnumerable<string> pathFolders,
        Func<string, bool> fileExists,
        Func<string, IEnumerable<string>> directoryEnumerator,
        IEnumerable<string> knownFolders,
        OperatingSystemKind? os = null)
    {
        ArgumentNullException.ThrowIfNull(pathFolders);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryEnumerator);
        ArgumentNullException.ThrowIfNull(knownFolders);

        var kind = os ?? PlatformDetector.DetectOs();
        var executable = kind == OperatingSystemKind.Windows ? "ffmpeg.exe" : "ffmpeg";

        // Steps 1 and 2 are authoritative: the candidate is returned even when the file is missing,
        // and the info step reports E-FF-02. There is no fall-through to PATH.
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            return new FfmpegCandidate(ffmpegPath, FfmpegSource.Argument);
        }

        if (!string.IsNullOrEmpty(envVar))
        {
            return new FfmpegCandidate(envVar, FfmpegSource.Environment);
        }

        foreach (var folder in pathFolders.Where(Path.IsPathRooted))
        {
            var candidate = Path.Combine(folder, executable);
            if (fileExists(candidate))
            {
                return new FfmpegCandidate(candidate, FfmpegSource.Path);
            }
        }

        foreach (var folder in knownFolders)
        {
            var candidate = Path.Combine(folder, executable);
            if (fileExists(candidate))
            {
                return new FfmpegCandidate(candidate, FfmpegSource.KnownFolder);
            }

            if (kind == OperatingSystemKind.Windows
                && string.Equals(Path.GetFileName(folder), "Packages", StringComparison.OrdinalIgnoreCase)
                && FindWinGetBuild(folder, executable, fileExists, directoryEnumerator) is { } found)
            {
                return new FfmpegCandidate(found, FfmpegSource.KnownFolder);
            }
        }

        return null;
    }

    /// <summary>Packages\Gyan.FFmpeg_*\ffmpeg-*\bin\ffmpeg.exe</summary>
    private static string? FindWinGetBuild(
        string packagesFolder,
        string executable,
        Func<string, bool> fileExists,
        Func<string, IEnumerable<string>> directoryEnumerator)
    {
        foreach (var package in directoryEnumerator(packagesFolder).Where(d => NameStartsWith(d, "Gyan.FFmpeg_")))
        {
            foreach (var build in directoryEnumerator(package).Where(d => NameStartsWith(d, "ffmpeg-")))
            {
                var candidate = Path.Combine(build, "bin", executable);
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool NameStartsWith(string directory, string prefix) =>
        Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
