using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;

namespace Drawplate.Services.Interaction;

public sealed class MenuResult
{
    public required EncoderKind? Encoder { get; init; }
    public required int? Quality { get; init; }
    public required SizeCap? MaxSize { get; init; }
    public required Message? Error { get; init; }
}

public sealed class MenuService
{
    private readonly IPrompt _prompt;

    public MenuService(IPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _prompt = prompt;
    }

    public MenuResult Run(ImmutableArray<EncoderKind> workingEncoders, int currentQuality, SizeCap currentMaxSize)
    {
        if (!_prompt.IsInteractive)
        {
            throw new InvalidOperationException("Interactive mode needs a terminal.");
        }

        var ordered = workingEncoders
            .Distinct()
            .ToImmutableArray();
        var choices = ImmutableArray.Create("auto")
            .AddRange(ordered.Select(EncoderSets.Id));
        var label = $"Encoder ({string.Join(", ", choices.Select((choice, index) => $"{index + 1} {choice}"))})";
        var encoderChoice = _prompt.Ask(label, "1");
        if (!int.TryParse(encoderChoice, NumberStyles.None, CultureInfo.InvariantCulture, out var encoderIndex)
            || encoderIndex < 1 || encoderIndex > choices.Length)
        {
            return Invalid(new Message("E-MENU-01", "Invalid choice.", null, 2));
        }

        EncoderKind? encoder = encoderIndex == 1 ? null : ordered[encoderIndex - 2];

        var qualityChoice = _prompt.Ask("Quality (1 to 100)", currentQuality.ToString(CultureInfo.InvariantCulture));
        var quality = currentQuality;
        if (!string.IsNullOrEmpty(qualityChoice)
            && (!int.TryParse(qualityChoice, NumberStyles.None, CultureInfo.InvariantCulture, out quality)
                || !QualityScale.IsValid(quality)))
        {
            return Invalid(Messages.E_USAGE_05());
        }

        var currentSize = currentMaxSize switch
        {
            SizeCap.Sd => "sd",
            SizeCap.Hd => "hd",
            SizeCap.FullHd => "fullhd",
            SizeCap.Uhd4K => "4k",
            _ => "none",
        };
        var sizeChoice = _prompt.Ask("Max size (sd, hd, fullhd, 4k, none)", currentSize);
        if (!string.IsNullOrEmpty(sizeChoice) && !SizeCaps.TryParse(sizeChoice, out _))
        {
            return Invalid(new Message("E-MENU-02", $"Invalid max size '{sizeChoice}'.",
                "Choose one of: sd, hd, fullhd, 4k, none.", 2));
        }

        _ = SizeCaps.TryParse(sizeChoice, out var maxSize);
        return new MenuResult { Encoder = encoder, Quality = quality, MaxSize = maxSize, Error = null };
    }

    private static MenuResult Invalid(Message error) =>
        new() { Encoder = null, Quality = null, MaxSize = null, Error = error };
}
