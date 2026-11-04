using System;
using System.Text.RegularExpressions;

namespace Drawplate.Models.Ffmpeg;

public static class FfmpegBannerParser
{
    private static readonly Regex VersionRegex = new(@"ffmpeg version (?<version>\S+)", RegexOptions.Compiled);
    private static readonly Regex MasterRegex = new(@"^N-\d+-g[0-9a-f]+", RegexOptions.Compiled);
    private static readonly Regex EncoderRegex = new(@"^\s*[VAS][A-Z.]{5}\s+(\S+)", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"Duration:\s*(?<h>\d+):(?<m>\d{2}):(?<s>\d{2})\.(?<ms>\d{2})", RegexOptions.Compiled);
    private static readonly Regex VideoStreamRegex = new(@"Stream\s+#\d+:\d+.*?Video:", RegexOptions.Compiled);

    public static FfmpegVersion ParseVersion(string versionOutput)
    {
        ArgumentNullException.ThrowIfNull(versionOutput);

        var match = VersionRegex.Match(versionOutput);
        if (!match.Success)
        {
            return new FfmpegVersion(new Version(0, 0), false, versionOutput);
        }

        var raw = match.Groups["version"].Value;
        var isMaster = MasterRegex.IsMatch(raw);

        Version version;
        if (isMaster)
        {
            version = new Version(99, 0);
        }
        else
        {
            // Strip build suffixes like -3ubuntu5, -39-g7adfbe40dc-20261031, -full_build-www.gyan.dev
            var clean = raw;
            var dash = clean.IndexOf('-');
            if (dash >= 0)
            {
                clean = clean[..dash];
            }
            version = new Version(clean);
        }

        return new FfmpegVersion(version, isMaster, versionOutput);
    }

    public static ImmutableHashSet<string> ParseEncoders(string encodersOutput)
    {
        ArgumentNullException.ThrowIfNull(encodersOutput);

        var encoders = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in encodersOutput.Split('\n'))
        {
            var match = EncoderRegex.Match(line);
            if (match.Success)
            {
                encoders = encoders.Add(match.Groups[1].Value);
            }
        }

        return encoders;
    }

    public static TimeSpan? ParseDuration(string stderr)
    {
        if (stderr is null)
        {
            return null;
        }

        var match = DurationRegex.Match(stderr);
        if (!match.Success)
        {
            return null;
        }

        var h = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var m = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var s = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        var ms = int.Parse(match.Groups["ms"].Value, CultureInfo.InvariantCulture);
        return new TimeSpan(h, m, s) + TimeSpan.FromMilliseconds(ms * 10);
    }

    public static bool HasVideoStream(string stderr)
    {
        if (stderr is null)
        {
            return false;
        }

        return VideoStreamRegex.IsMatch(stderr);
    }
}
