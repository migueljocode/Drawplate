using System;
using System.Collections.Immutable;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;

namespace Drawplate.Console.Cli;

internal sealed record ParsedCommand
{
    public required CommandKind Kind { get; init; }
    public required RawOptions? Options { get; init; }
    public required ImmutableArray<string> Files { get; init; }
    public required Message? Error { get; init; }

    public static ParsedCommand Convert(RawOptions options, ImmutableArray<string> files) =>
        new() { Kind = CommandKind.Convert, Options = options, Files = files, Error = null };

    public static ParsedCommand Setup(RawOptions options) =>
        new() { Kind = CommandKind.Setup, Options = options, Files = [], Error = null };

    public static ParsedCommand Doctor() =>
        new() { Kind = CommandKind.Doctor, Options = null, Files = [], Error = null };

    public static ParsedCommand Help() =>
        new() { Kind = CommandKind.Help, Options = null, Files = [], Error = null };

    public static ParsedCommand Version() =>
        new() { Kind = CommandKind.Version, Options = null, Files = [], Error = null };

    public static ParsedCommand ErrorResult(Message error) =>
        new() { Kind = CommandKind.Error, Options = null, Files = [], Error = error };
}

internal enum CommandKind
{
    Convert,
    Setup,
    Doctor,
    Help,
    Version,
    Error
}
