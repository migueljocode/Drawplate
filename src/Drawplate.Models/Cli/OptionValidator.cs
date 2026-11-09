using System;
using System.Globalization;
using System.Linq;

using Drawplate.Models.Encoding;

namespace Drawplate.Models.Cli;

public static class OptionValidator
{
    public static (ConvertOptions? Options, Message? Error) Validate(RawOptions raw, OperatingSystemKind os)
    {
        ArgumentNullException.ThrowIfNull(raw);

        // Encoder validation
        var encoderId = (raw.Encoder ?? "auto").ToLowerInvariant();
        if (raw.Cpu)
        {
            encoderId = "cpu";
        }

        EncoderKind? encoder = null;
        if (encoderId != "auto")
        {
            var parse = EncoderSets.Parse(encoderId, os);
            if (parse.Status == ParseStatus.UnknownEverywhere)
            {
                var list = string.Join(", ", EncoderSets.For(os).Select(EncoderSets.Id));
                return (null, Messages.E_USAGE_04(encoderId, list));
            }

            if (parse.Status == ParseStatus.WrongOs)
            {
                var list = string.Join(", ", EncoderSets.For(os).Select(EncoderSets.Id));
                var osName = os switch
                {
                    OperatingSystemKind.Linux => "Linux",
                    OperatingSystemKind.Windows => "Windows",
                    OperatingSystemKind.MacOS => "macOS",
                    _ => os.ToString(),
                };
                return (null, Messages.E_USAGE_12(encoderId, osName, list));
            }

            encoder = parse.Kind;
        }

        // Quality validation
        int quality;
        if (!QualityScale.TryParse(raw.Quality, out quality))
        {
            return (null, Messages.E_USAGE_05());
        }

        // Max size validation
        SizeCap cap;
        if (string.IsNullOrEmpty(raw.MaxSize))
        {
            cap = SizeCap.None;
        }
        else if (!SizeCaps.TryParse(raw.MaxSize, out cap))
        {
            return (null, Messages.E_USAGE_06(raw.MaxSize));
        }

        // -i with files
        if (!string.IsNullOrEmpty(raw.InputFolder) && raw.Files.Length > 0)
        {
            return (null, Messages.E_USAGE_11());
        }

        var options = new ConvertOptions
        {
            Encoder = encoder,
            Quality = quality,
            MaxSize = cap,
            InputFolder = raw.InputFolder,
            Files = raw.Files,
            NoTimestamps = raw.NoTimestamps,
            OutputFolder = raw.OutputFolder,
        };

        return (options, null);
    }
}
