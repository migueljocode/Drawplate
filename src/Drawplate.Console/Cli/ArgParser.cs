using System;
using System.Collections.Immutable;
using System.Linq;

using Drawplate.Models.Cli;

namespace Drawplate.Console.Cli;

internal static class ArgParser
{
    internal static ParsedCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new RawOptions();
        var files = ImmutableArray.CreateBuilder<string>();
        var i = 0;

        while (i < args.Length)
        {
            var arg = args[i];

            if (arg == "--")
            {
                i++;
                while (i < args.Length)
                {
                    files.Add(args[i]);
                    i++;
                }
                break;
            }

            if (arg.StartsWith("--"))
            {
                var result = ParseLong(arg, options);
                if (result is { } msg)
                {
                    return ParsedCommand.ErrorResult(msg);
                }
                i++;
                continue;
            }

            if (arg.StartsWith('-') && arg.Length > 1)
            {
                var result = ParseShort(arg, options, args, ref i);
                if (result is { } msg)
                {
                    return ParsedCommand.ErrorResult(msg);
                }
                continue;
            }

            files.Add(arg);
            i++;
        }

        return ParsedCommand.Convert(options, files.ToImmutable());
    }

    private static Message? ParseLong(string arg, RawOptions options)
    {
        var name = arg[2..];
        var value = string.Empty;
        if (name.Contains('='))
        {
            var parts = name.Split('=', 2);
            name = parts[0];
            value = parts[1];
        }

        var entry = OptionTable.Entries.FirstOrDefault(e => e.LongNames.Any(l => l == $"--{name}"));
        if (entry is null)
        {
            return Messages.E_USAGE_01_Long(arg);
        }

        if (!entry.TakesValue && value.Length > 0)
        {
            return Messages.E_USAGE_03(name);
        }

        if (entry.TakesValue && value.Length == 0)
        {
            return Messages.E_USAGE_02($"--{name}");
        }

        entry.Setter(options, value);
        return null;
    }

    private static Message? ParseShort(string arg, RawOptions options, string[] allArgs, ref int index)
    {
        var chars = arg[1..];
        var i = 0;

        while (i < chars.Length)
        {
            var c = chars[i];
            var entry = OptionTable.Entries.FirstOrDefault(e => e.ShortName == c.ToString());
            if (entry is null)
            {
                return Messages.E_USAGE_01_Short(c);
            }

            if (!entry.TakesValue)
            {
                entry.Setter(options, string.Empty);
                i++;
                continue;
            }

            // Takes value: either attached (-q50) or next arg (-q 50)
            if (i + 1 < chars.Length)
            {
                entry.Setter(options, chars[(i + 1)..]);
                index++;
                return null;
            }

            // Value is next argument
            index++;
            if (index >= allArgs.Length)
            {
                return Messages.E_USAGE_02(arg);
            }
            entry.Setter(options, allArgs[index]);
            index++;
            return null;
        }

        index++;
        return null;
    }
}
