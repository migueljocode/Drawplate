using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Drawplate.Services.Ffmpeg;

public static class FfmpegLocator
{
    public static FfmpegCandidate? Locate(
        string? ffmpegPath,
        string? envVar,
        IEnumerable<string> pathFolders,
        Func<string, bool> fileExists,
        Func<string, IEnumerable<string>> directoryEnumerator,
        string[] knownFolders)
    {
        ArgumentNullException.ThrowIfNull(pathFolders);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryEnumerator);
        ArgumentNullException.ThrowIfNull(knownFolders);
        // Step 1: --ffmpeg-path (authoritative, no fallthrough)
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            if (fileExists(ffmpegPath))
            {
                return new FfmpegCandidate(ffmpegPath, FfmpegSource.Argument);
            }
            return null;
        }

        // Step 2: DRAWPLATE_FFMPEG (authoritative, no fallthrough)
        if (!string.IsNullOrEmpty(envVar))
        {
            if (fileExists(envVar))
            {
                return new FfmpegCandidate(envVar, FfmpegSource.Environment);
            }
            return null;
        }

        // Step 3: PATH
        foreach (var folder in pathFolders)
        {
            var candidate = Path.Combine(folder, GetExecutableName());
            if (fileExists(candidate))
            {
                return new FfmpegCandidate(candidate, FfmpegSource.Path);
            }
        }

        // Step 4: Known folders
        foreach (var folder in knownFolders)
        {
            var candidate = Path.Combine(folder, GetExecutableName());
            if (fileExists(candidate))
            {
                return new FfmpegCandidate(candidate, FfmpegSource.KnownFolder);
            }

            // Glob for Gyan.FFmpeg_*\ffmpeg-*\bin on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var globMatches = directoryEnumerator(folder);
                foreach (var match in globMatches)
                {
                    var binCandidate = Path.Combine(match, GetExecutableName());
                    if (fileExists(binCandidate))
                    {
                        return new FfmpegCandidate(binCandidate, FfmpegSource.KnownFolder);
                    }
                }
            }
        }

        return null;
    }

    private static string GetExecutableName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
}
