using System;
using System.Linq;

using Drawplate.Models.Encoding;

namespace Drawplate.Services.Interaction;

public sealed class MenuResult
{
    public required EncoderKind? Encoder { get; init; }
    public required int? Quality { get; init; }
    public required SizeCap? MaxSize { get; init; }
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

        var encoderOptions = workingEncoders
            .OrderBy(k => k == EncoderKind.Cpu ? 1 : 0)
            .ThenBy(k => k)
            .ToImmutableArray();

        var encoderChoice = _prompt.Ask("Encoder", "1");
        var encoder = encoderOptions.Length > 0 && int.TryParse(encoderChoice, out var idx)
            && idx >= 1 && idx <= encoderOptions.Length
            ? encoderOptions[idx - 1]
            : (encoderOptions.Length > 0 ? (EncoderKind?)encoderOptions[0] : null);

        var qualityChoice = _prompt.Ask("Quality", currentQuality.ToString());
        int quality = currentQuality;
        if (!string.IsNullOrEmpty(qualityChoice) && int.TryParse(qualityChoice, out var q) && QualityScale.IsValid(q))
        {
            quality = q;
        }

        var sizeChoice = _prompt.Ask("Max size", currentMaxSize == SizeCap.None ? "none" : currentMaxSize.ToString());
        SizeCap maxSize = currentMaxSize;
        if (!string.IsNullOrEmpty(sizeChoice) && SizeCaps.TryParse(sizeChoice, out var cap))
        {
            maxSize = cap;
        }

        return new MenuResult { Encoder = encoder, Quality = quality, MaxSize = maxSize };
    }
}
