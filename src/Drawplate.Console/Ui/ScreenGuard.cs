using System;
using System.IO;

namespace Drawplate.Console.Ui;

/// <summary>Manages alternate screen buffer, mouse wheel reporting, and cursor visibility with guaranteed restoration.</summary>
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

    public static ScreenGuard? TryEnter(TextWriter output, Theme theme, bool enableMouse = true)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(theme);
        if (!theme.CanRedraw)
        {
            return null;
        }

        try
        {
            // Enter alternate buffer + hide cursor + enable alternate scroll & SGR mouse wheel events.
            var init = enableMouse
                ? "\u001b[?1049h\u001b[?25l\u001b[?1007h\u001b[?1000h\u001b[?1006h"
                : "\u001b[?1049h\u001b[?25l";
            output.Write(init);
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
            target.Write("\u001b[0m\u001b[?1006l\u001b[?1000l\u001b[?1007l\u001b[?25h\u001b[?1049l");
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
