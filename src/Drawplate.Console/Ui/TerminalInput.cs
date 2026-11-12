using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Drawplate.Console.Ui;

internal enum UiKeyAction
{
    None,
    Up,
    Down,
    Left,
    Right,
    PageUp,
    PageDown,
    WheelUp,
    WheelDown,
    Home,
    End,
    Enter,
    Quit,
    Search,
    NextMatch,
    PrevMatch,
    Backspace,
    Char,
}

internal readonly record struct UiInputEvent(UiKeyAction Action, char KeyChar = '\0');

/// <summary>Decodes console keys, ANSI CSI/SS3 navigation sequences, and SGR/X10 mouse wheel events safely.</summary>
internal static class TerminalInput
{
    public static bool KeyAvailableSafe()
    {
        try
        {
            return global::System.Console.KeyAvailable;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static ConsoleKeyInfo? ReadKeySafe()
    {
        try
        {
            return global::System.Console.ReadKey(intercept: true);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static UiInputEvent? ReadEvent()
    {
        var key = ReadKeySafe();
        if (key is null)
        {
            return null;
        }

        var k = key.Value;
        if (k.Key == ConsoleKey.Escape || k.KeyChar == '\u001b')
        {
            // Disambiguate genuine Esc keypress vs incoming ANSI escape / mouse wheel sequence.
            if (!WaitForEscapeContinuation())
            {
                return new UiInputEvent(UiKeyAction.Quit);
            }

            var seq = ReadEscapeSequence();
            return new UiInputEvent(DecodeEscapeSequence(seq));
        }

        return DecodeConsoleKey(k);
    }

    internal static UiInputEvent DecodeConsoleKey(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.UpArrow || k.KeyChar == 'k')
        {
            return new UiInputEvent(UiKeyAction.Up, k.KeyChar);
        }

        if (k.Key == ConsoleKey.DownArrow || k.KeyChar == 'j')
        {
            return new UiInputEvent(UiKeyAction.Down, k.KeyChar);
        }

        if (k.Key == ConsoleKey.LeftArrow || k.KeyChar == 'h')
        {
            return new UiInputEvent(UiKeyAction.Left, k.KeyChar);
        }

        if (k.Key == ConsoleKey.RightArrow || k.KeyChar == 'l')
        {
            return new UiInputEvent(UiKeyAction.Right, k.KeyChar);
        }

        if (k.Key is ConsoleKey.PageDown or ConsoleKey.Spacebar || k.KeyChar == 'f')
        {
            return new UiInputEvent(UiKeyAction.PageDown, k.KeyChar);
        }

        if (k.Key == ConsoleKey.PageUp || k.KeyChar == 'b')
        {
            return new UiInputEvent(UiKeyAction.PageUp, k.KeyChar);
        }

        if (k.Key == ConsoleKey.Home || k.KeyChar == 'g')
        {
            return new UiInputEvent(UiKeyAction.Home, k.KeyChar);
        }

        if (k.Key == ConsoleKey.End || k.KeyChar == 'G')
        {
            return new UiInputEvent(UiKeyAction.End, k.KeyChar);
        }

        if (k.Key == ConsoleKey.Enter)
        {
            return new UiInputEvent(UiKeyAction.Enter, '\n');
        }

        if (k.Key == ConsoleKey.Backspace)
        {
            return new UiInputEvent(UiKeyAction.Backspace);
        }

        if (k.Key == ConsoleKey.Q || k.KeyChar == 'q')
        {
            return new UiInputEvent(UiKeyAction.Quit, k.KeyChar);
        }

        if (k.KeyChar == '/')
        {
            return new UiInputEvent(UiKeyAction.Search, '/');
        }

        if (k.KeyChar == 'n')
        {
            return new UiInputEvent(UiKeyAction.NextMatch, 'n');
        }

        if (k.KeyChar == 'N')
        {
            return new UiInputEvent(UiKeyAction.PrevMatch, 'N');
        }

        return char.IsControl(k.KeyChar)
            ? new UiInputEvent(UiKeyAction.None)
            : new UiInputEvent(UiKeyAction.Char, k.KeyChar);
    }

    /// <summary>Decodes the tail of an ANSI escape sequence (after the leading ESC byte).</summary>
    internal static UiKeyAction DecodeEscapeSequence(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
        {
            return UiKeyAction.None;
        }

        if (sequence is "[A" or "OA")
        {
            return UiKeyAction.Up;
        }

        if (sequence is "[B" or "OB")
        {
            return UiKeyAction.Down;
        }

        if (sequence is "[C" or "OC")
        {
            return UiKeyAction.Right;
        }

        if (sequence is "[D" or "OD")
        {
            return UiKeyAction.Left;
        }

        if (sequence is "[H" or "OH" or "[1~")
        {
            return UiKeyAction.Home;
        }

        if (sequence is "[F" or "OF" or "[4~")
        {
            return UiKeyAction.End;
        }

        if (sequence is "[5~")
        {
            return UiKeyAction.PageUp;
        }

        if (sequence is "[6~")
        {
            return UiKeyAction.PageDown;
        }

        // SGR mouse tracking: "[<button;x;yM" or "[<button;x;ym"
        if (sequence.StartsWith("[<", StringComparison.Ordinal))
        {
            var semi = sequence.IndexOf(';', 2);
            if (semi > 2 && int.TryParse(sequence.AsSpan(2, semi - 2), out var button))
            {
                if ((button & 64) != 0)
                {
                    return (button & 1) == 0 ? UiKeyAction.WheelUp : UiKeyAction.WheelDown;
                }
            }

            return UiKeyAction.None;
        }

        // Legacy X10 mouse tracking: "[M" followed by 3 raw bytes (button + 32, x + 32, y + 32)
        if (sequence.StartsWith("[M", StringComparison.Ordinal) && sequence.Length >= 3)
        {
            var rawButton = sequence[2] - 32;
            if ((rawButton & 64) != 0)
            {
                return (rawButton & 1) == 0 ? UiKeyAction.WheelUp : UiKeyAction.WheelDown;
            }

            return UiKeyAction.None;
        }

        return UiKeyAction.None;
    }

    private static bool WaitForEscapeContinuation()
    {
        for (var i = 0; i < 5; i++)
        {
            if (KeyAvailableSafe())
            {
                return true;
            }

            Thread.Sleep(4);
        }

        return KeyAvailableSafe();
    }

    private static string ReadEscapeSequence()
    {
        var sb = new StringBuilder(16);
        var first = ReadKeySafe();
        if (first is null)
        {
            return string.Empty;
        }

        var ch = first.Value.KeyChar;
        if (ch == '\0')
        {
            return string.Empty;
        }

        sb.Append(ch);
        if (ch == 'O')
        {
            if (KeyAvailableSafe() && ReadKeySafe() is { } ss3)
            {
                sb.Append(ss3.KeyChar);
            }

            return sb.ToString();
        }

        if (ch != '[')
        {
            return sb.ToString();
        }

        // X10 mouse event: next 3 chars are button, col, row.
        if (KeyAvailableSafe() && ReadKeySafe() is { } next)
        {
            sb.Append(next.KeyChar);
            if (next.KeyChar == 'M')
            {
                for (var i = 0; i < 3; i++)
                {
                    if (!WaitForEscapeContinuation() || ReadKeySafe() is not { } mByte)
                    {
                        break;
                    }

                    sb.Append(mByte.KeyChar);
                }

                return sb.ToString();
            }

            if (IsCsiTerminator(next.KeyChar))
            {
                return sb.ToString();
            }
        }

        while (sb.Length < 32 && WaitForEscapeContinuation())
        {
            var c = ReadKeySafe();
            if (c is null)
            {
                break;
            }

            sb.Append(c.Value.KeyChar);
            if (IsCsiTerminator(c.Value.KeyChar))
            {
                break;
            }
        }

        return sb.ToString();
    }

    private static bool IsCsiTerminator(char c) => c is >= '@' and <= '~';
}
