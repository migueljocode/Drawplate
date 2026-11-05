using System;
using System.Threading;

namespace Drawplate.Console.Signals;

internal sealed class SignalHandler : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public CancellationToken Token => _cts.Token;

    public SignalHandler()
    {
        try
        {
            global::System.Console.CancelKeyPress += OnCancelKeyPress;
        }
        catch (InvalidOperationException) { }
        catch (PlatformNotSupportedException) { }
    }

    private static void OnCancelKeyPress(object? sender, global::System.ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { global::System.Console.CancelKeyPress -= OnCancelKeyPress; } catch (InvalidOperationException) { }
        _cts.Dispose();
    }
}
