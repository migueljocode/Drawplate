using System;

using Drawplate.Models.Failures;

namespace Drawplate.Models.Failures;

public static class FailureClassifier
{
    public static FailureKind Classify(string stderrTail)
    {
        ArgumentNullException.ThrowIfNull(stderrTail);

        var lower = stderrTail.ToLowerInvariant();

        if (lower.Contains("invalid data found when processing input"))
        {
            return FailureKind.InvalidInput;
        }

        if (lower.Contains("unknown encoder") || lower.Contains("encoder not found"))
        {
            return FailureKind.EncoderUnavailable;
        }

        if (lower.Contains("driver does not support the required nvenc api version"))
        {
            return FailureKind.NvencDriverTooOld;
        }

        if (lower.Contains("permission denied") &&
            (lower.Contains("/dev/dri") || lower.Contains("/dev/nvidia")))
        {
            return FailureKind.DeviceAccess;
        }

        return FailureKind.Unknown;
    }

    public static (string? Required, string? Found, string? MinimumDriver) ParseNvenc(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string? required = null;
        string? found = null;
        string? minimumDriver = null;

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("Required:", StringComparison.Ordinal) &&
                trimmed.Contains("Found:", StringComparison.Ordinal))
            {
                var reqPart = trimmed.Substring(trimmed.IndexOf("Required:", StringComparison.Ordinal) + "Required:".Length);
                var foundPart = reqPart.Substring(reqPart.IndexOf("Found:", StringComparison.Ordinal) + "Found:".Length);
                required = reqPart[..reqPart.IndexOf("Found:", StringComparison.Ordinal)].Trim();
                found = foundPart.Trim();
            }
            else if (trimmed.Contains("minimum required Nvidia driver", StringComparison.Ordinal) ||
                     trimmed.Contains("minimum required nvidia driver", StringComparison.Ordinal))
            {
                var digits = System.Text.RegularExpressions.Regex.Match(trimmed, @"\d+(\.\d+)*");
                if (digits.Success)
                {
                    minimumDriver = digits.Value;
                }
            }
        }

        return (required, found, minimumDriver);
    }

    public static string ShortReason(FailureKind kind, string tail)
    {
        ArgumentNullException.ThrowIfNull(tail);
        return kind switch
        {
            FailureKind.InvalidInput => "invalid or corrupt input",
            FailureKind.NoVideoStream => "no video stream",
            FailureKind.EncoderUnavailable => "encoder failed: " + LastNonEmptyLine(tail),
            FailureKind.NvencDriverTooOld => "encoder failed: " + LastNonEmptyLine(tail),
            FailureKind.DeviceAccess => "encoder failed: " + LastNonEmptyLine(tail),
            _ => "encoder failed: " + LastNonEmptyLine(tail),
        };
    }

    private static string LastNonEmptyLine(string tail)
    {
        var last = "";
        foreach (var line in tail.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                last = trimmed;
            }
        }
        return last;
    }
}
