using System;
using System.Collections.Immutable;
using System.Linq;

using Drawplate.Models.Cli;

namespace Drawplate.Models.Encoding;

public sealed record EncoderChainResult
{
    public required ImmutableArray<EncoderKind> Chain { get; init; }
    public required Message? Error { get; init; }
}

public static class EncoderChain
{
    public static EncoderChainResult Build(
        string? requested,
        OperatingSystemKind os,
        ImmutableArray<EncoderKind> workingEncoders)
    {
        if (requested is null || requested == "auto")
        {
            var chain = workingEncoders;
            if (chain.Contains(EncoderKind.Cpu))
            {
                chain = chain.Remove(EncoderKind.Cpu).Add(EncoderKind.Cpu);
            }
            return new EncoderChainResult { Chain = chain, Error = null };
        }

        var result = EncoderSets.Parse(requested, os);
        if (!result.IsOk)
        {
            var list = string.Join(", ", EncoderSets.For(os).Select(EncoderSets.Id));
            var osName = os switch
            {
                OperatingSystemKind.Linux => "Linux",
                OperatingSystemKind.Windows => "Windows",
                OperatingSystemKind.MacOS => "macOS",
                _ => os.ToString(),
            };
            return new EncoderChainResult
            {
                Chain = [],
                Error = result.Status == ParseStatus.UnknownEverywhere
                    ? Messages.E_USAGE_04(requested, list)
                    : Messages.E_USAGE_12(requested, osName, list),
            };
        }

        var requestedKind = result.Kind!.Value;
        if (!workingEncoders.Contains(requestedKind))
        {
            return new EncoderChainResult
            {
                Chain = [],
                Error = Messages.E_RUN_02(EncoderSets.Id(requestedKind)),
            };
        }

        var chainBuilder = ImmutableArray.Create(requestedKind);
        if (workingEncoders.Contains(EncoderKind.Cpu) && requestedKind != EncoderKind.Cpu)
        {
            chainBuilder = chainBuilder.Add(EncoderKind.Cpu);
        }

        return new EncoderChainResult { Chain = chainBuilder, Error = null };
    }
}
