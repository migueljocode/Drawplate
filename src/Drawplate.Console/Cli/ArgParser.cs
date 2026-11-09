using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

using Drawplate.Models.Cli;

[assembly: InternalsVisibleTo("Drawplate.Tests")]

namespace Drawplate.Console.Cli;

/// <summary>getopts-style parsing (D.1). Help and version return as soon as they are reached.</summary>
internal static class ArgParser
{
    internal static ParsedCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new RawOptions();
        var files = ImmutableArray.CreateBuilder<string>();
        var kind = CommandKind.Convert;
        var i = 0;

        if (args.Length > 0 && args[0] is "setup" or "doctor")
        {
            kind = args[0] == "setup" ? CommandKind.Setup : CommandKind.Doctor;
            i = 1;
        }

        while (i < args.Length)
        {
            var arg = args[i];

            if (arg == "--")
            {
                for (i++; i < args.Length; i++)
                {
                    if (kind != CommandKind.Convert)
                    {
                        return ParsedCommand.ErrorResult(Messages.E_USAGE_01_Long(args[i]));
                    }
                    files.Add(args[i]);
                }
                break;
            }

            Message? error;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = ParseLong(args, ref i, options);
            }
            else if (arg.Length > 1 && arg[0] == '-')
            {
                error = ParseShort(args, ref i, options);
            }
            else
            {
                if (kind != CommandKind.Convert)
                {
                    return ParsedCommand.ErrorResult(Messages.E_USAGE_01_Long(arg));
                }
                files.Add(arg);
                i++;
                continue;
            }

            if (error is not null)
            {
                return ParsedCommand.ErrorResult(error);
            }
            if (options.Help)
            {
                return ParsedCommand.Help();
            }
            if (options.Version)
            {
                return ParsedCommand.Version();
            }
        }

        return kind switch
        {
            CommandKind.Setup => ParsedCommand.Setup(options),
            CommandKind.Doctor => ParsedCommand.Doctor(options),
            _ => ParsedCommand.Convert(WithFiles(options, files.ToImmutable()), files.ToImmutable()),
        };
    }

    /// <summary>Positional files belong to the options too: OptionValidator reads them from there.</summary>
    private static RawOptions WithFiles(RawOptions options, ImmutableArray<string> files)
    {
        options.Files = files;
        return options;
    }

    private static Message? ParseLong(string[] args, ref int index, RawOptions options)
    {
        var arg = args[index];
        var body = arg[2..];
        string? inlineValue = null;
        var equals = body.IndexOf('=', StringComparison.Ordinal);
        if (equals >= 0)
        {
            inlineValue = body[(equals + 1)..];
            body = body[..equals];
        }

        var name = "--" + body;
        var entry = OptionTable.Entries.FirstOrDefault(e => e.LongNames.Contains(name));
        if (entry is null)
        {
            return Messages.E_USAGE_01_Long(arg);
        }

        index++;
        if (!entry.TakesValue)
        {
            if (inlineValue is not null)
            {
                return Messages.E_USAGE_03(body);
            }
            entry.Setter(options, string.Empty);
            return null;
        }

        string value;
        if (inlineValue is not null)
        {
            value = inlineValue;
        }
        else if (index < args.Length)
        {
            value = args[index];
            index++;
        }
        else
        {
            return Messages.E_USAGE_02(name);
        }

        if (value.Length == 0)
        {
            return Messages.E_USAGE_02(name);
        }

        entry.Setter(options, value);
        return null;
    }

    private static Message? ParseShort(string[] args, ref int index, RawOptions options)
    {
        var cluster = args[index][1..];
        index++;

        for (var k = 0; k < cluster.Length; k++)
        {
            var letter = cluster[k];
            var entry = OptionTable.Entries.FirstOrDefault(e => e.ShortName == letter.ToString());
            if (entry is null)
            {
                return Messages.E_USAGE_01_Short(letter);
            }

            if (!entry.TakesValue)
            {
                entry.Setter(options, string.Empty);
                if (options.Help || options.Version)
                {
                    return null;
                }
                continue;
            }

            // Value is the rest of the cluster (-q50) or the next argument (-q 50).
            if (k + 1 < cluster.Length)
            {
                entry.Setter(options, cluster[(k + 1)..]);
                return null;
            }

            if (index >= args.Length)
            {
                return Messages.E_USAGE_02("-" + letter);
            }

            entry.Setter(options, args[index]);
            index++;
            return null;
        }

        return null;
    }
}
