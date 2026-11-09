using System;

using Drawplate.Console.Signals;

namespace Drawplate.Console;

internal static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var signals = new SignalHandler();
        return AppHost.RunGuarded(() => AppHost.Run(args, global::System.Console.Out, global::System.Console.Error, signals.Token));
    }
}
