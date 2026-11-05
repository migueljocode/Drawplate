using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Setup;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Setup;

public sealed record InstallRunResult(bool Started, int? ExitCode);

public sealed class InstallRunner
{
    private readonly IProcessLauncher _launcher;

    public InstallRunner(IProcessLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
    }

    public async Task<InstallRunResult> RunAsync(
        InstallSuggestion suggestion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        if (suggestion.Executable is null || !suggestion.RunnableByApp)
        {
            throw new InvalidOperationException("Suggestion is not runnable by the app.");
        }

        if (suggestion.Arguments.IsEmpty)
        {
            return new InstallRunResult(false, null);
        }

        try
        {
            var process = _launcher.Start(
                suggestion.Executable,
                suggestion.Arguments,
                new ProcessOptions(InheritConsole: true));
            try
            {
                var exitCode = await process.WaitAsync(ct).ConfigureAwait(false);
                return new InstallRunResult(true, exitCode);
            }
            finally
            {
                await process.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        return new InstallRunResult(false, null);
    }
}
