using System.Reflection;

namespace Drawplate.Console;

internal static class Program
{
    public static int Main(string[] args)
    {
        System.ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-V"))
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? "0.0.0";
            var plus = version.IndexOf('+');
            if (plus >= 0)
            {
                version = version[..plus];
            }
            global::System.Console.WriteLine($"drawplate {version}");
            return 0;
        }

        if (args.Length > 0)
        {
            var arg = args[0];
            if (arg.StartsWith("--"))
            {
                global::System.Console.Error.WriteLine($"Unknown option '{arg}'.");
            }
            else
            {
                global::System.Console.Error.WriteLine($"Unknown option -{arg[1]}.");
            }
            global::System.Console.Error.WriteLine("  Run 'drawplate --help' to see all options.");
            return 2;
        }

        return 0;
    }
}
