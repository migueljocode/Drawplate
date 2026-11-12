using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Drawplate.Console.Ui;

/// <summary>Enables Windows virtual terminal processing and UTF-8 output on interactive terminals, restoring on dispose.</summary>
internal sealed partial class ConsoleGuard : IDisposable
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    private readonly nint _handle;
    private readonly uint _originalMode;
    private readonly bool _restoreMode;
    private readonly Encoding? _originalEncoding;
    private bool _disposed;

    private ConsoleGuard(nint handle, uint originalMode, bool restoreMode, Encoding? originalEncoding, bool vtEnabled)
    {
        _handle = handle;
        _originalMode = originalMode;
        _restoreMode = restoreMode;
        _originalEncoding = originalEncoding;
        VtEnabled = vtEnabled;
    }

    public bool VtEnabled { get; }

    public static ConsoleGuard EnterInteractive()
    {
        Encoding? originalEncoding = null;
        try
        {
            if (!global::System.Console.IsOutputRedirected && global::System.Console.OutputEncoding.CodePage != 65001)
            {
                originalEncoding = global::System.Console.OutputEncoding;
                global::System.Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            }
        }
        catch (IOException)
        {
            originalEncoding = null;
        }
        catch (PlatformNotSupportedException)
        {
            originalEncoding = null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return new ConsoleGuard(0, 0, false, originalEncoding, vtEnabled: true);
        }

        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == 0 || handle == -1)
            {
                return new ConsoleGuard(0, 0, false, originalEncoding, vtEnabled: false);
            }

            if (GetConsoleMode(handle, out var mode) == 0)
            {
                return new ConsoleGuard(0, 0, false, originalEncoding, vtEnabled: false);
            }

            if ((mode & EnableVirtualTerminalProcessing) != 0)
            {
                return new ConsoleGuard(handle, mode, false, originalEncoding, vtEnabled: true);
            }

            if (SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing) != 0)
            {
                return new ConsoleGuard(handle, mode, true, originalEncoding, vtEnabled: true);
            }
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }

        return new ConsoleGuard(0, 0, false, originalEncoding, vtEnabled: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_restoreMode && OperatingSystem.IsWindows())
        {
            try
            {
                _ = SetConsoleMode(_handle, _originalMode);
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }

        if (_originalEncoding is not null)
        {
            try
            {
                global::System.Console.OutputEncoding = _originalEncoding;
            }
            catch (IOException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
