using System;
using System.IO;

namespace Drawplate.Console.Ui;

/// <summary>Manages alternate screen buffer and cursor visibility with guaranteed restoration on exit or signal.</summary>
internal sealed class ScreenGuard : IDisposable
{
    private readonly Action _restore;
    private readonly bool _entered;
    private bool _disposed;

    private ScreenGuard(Action restore, bool entered)
    {
        _restore = restore;
        _entered = entered;
    }

    public static ScreenGuard? TryEnter(TextWriter output, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(theme);
        if (!theme.CanRedraw)
        {
            return null;
        }

        try
        {
            output.Write("\u001b[?1049h\u001b[?25l");
            output.Flush();
            return new ScreenGuard(() => EmergencyRestore(output), entered: true);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void EmergencyRestore(TextWriter? output = null)
    {
        var target = output ?? global::System.Console.Out;
        try
        {
            target.Write("\u001b[0m\u001b[?25h\u001b[?1049l");
            target.Flush();
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_entered)
        {
            return;
        }

        _restore();
    }
}
