using System;

namespace Drawplate.Services.Processes;

/// <summary>The executable could not be started (missing, not executable, or the OS refused).</summary>
public sealed class ProcessStartFailedException : Exception
{
    public ProcessStartFailedException()
        : this(string.Empty, "The process could not be started.")
    {
    }

    public ProcessStartFailedException(string message)
        : this(string.Empty, message)
    {
    }

    public ProcessStartFailedException(string message, Exception inner)
        : this(string.Empty, message, inner)
    {
    }

    public ProcessStartFailedException(string executable, string message, Exception? inner = null)
        : base(message, inner)
    {
        Executable = executable;
    }

    public string Executable { get; }
}
