using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Drawplate.Console.Signals;

/// <summary>
/// F23: SIGINT, SIGTERM, SIGHUP, SIGQUIT and Ctrl-C cancel <see cref="Token"/> once. The signal is marked handled so the
/// host unwinds itself (kills the ffmpeg tree, deletes the partial file) and returns exit code 130.
/// </summary>
internal sealed class SignalHandler : IDisposable
{
    private static readonly PosixSignal[] Signals = [PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP, PosixSignal.SIGQUIT];

    private readonly CancellationTokenSource _cts = new();
    private readonly List<PosixSignalRegistration> _registrations = [];
    private int _fired;
    private bool _disposed;

    public CancellationToken Token => _cts.Token;

    public SignalHandler()
    {
        foreach (var signal in Signals)
        {
            try
            {
                _registrations.Add(PosixSignalRegistration.Create(signal, OnPosixSignal));
            }
            catch (PlatformNotSupportedException)
            {
                // The platform does not deliver this signal (for example SIGQUIT on Windows).
            }
            catch (ArgumentException)
            {
                // Same: the runtime rejected this signal value here.
            }
        }

        try
        {
            global::System.Console.CancelKeyPress += OnCancelKeyPress;
        }
        catch (InvalidOperationException)
        {
            // No console to hook; the signal registrations above still apply.
        }
        catch (PlatformNotSupportedException)
        {
            // Same.
        }
    }

    private void OnPosixSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        Trigger();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Trigger();
    }

    /// <summary>Only the first signal cancels; later ones are swallowed so cleanup can finish.</summary>
    private void Trigger()
    {
        if (Interlocked.Exchange(ref _fired, 1) != 0)
        {
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The host already finished and disposed us.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        try
        {
            global::System.Console.CancelKeyPress -= OnCancelKeyPress;
        }
        catch (InvalidOperationException)
        {
            // Nothing was hooked.
        }

        _cts.Dispose();
    }
}
