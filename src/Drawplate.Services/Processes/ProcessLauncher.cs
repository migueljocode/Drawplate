using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drawplate.Services.Processes;

public sealed class ProcessLauncher : IProcessLauncher
{
    private static readonly TimeSpan DrainLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(2);

    public IRunningProcess Start(string executable, IReadOnlyList<string> arguments, ProcessOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var opts = options ?? new ProcessOptions();

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = !opts.InheritConsole,
            RedirectStandardError = !opts.InheritConsole,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Win32Exception ex)
        {
            throw new ProcessStartFailedException(executable, ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ProcessStartFailedException(executable, ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new ProcessStartFailedException(executable, ex.Message, ex);
        }

        return process is null
            ? throw new ProcessStartFailedException(executable, "Process.Start returned no process.")
            : new RunningProcess(process, opts);
    }

    private sealed class RunningProcess : IRunningProcess
    {
        private readonly Process _process;
        private readonly ProcessOptions _options;
        private readonly RingBuffer _stderr = new(65536);
        private readonly Channel<string> _stdoutChannel = Channel.CreateUnbounded<string>();
        private readonly Task _stdoutReaderTask;
        private readonly Task _stderrReaderTask;
        private readonly CancellationTokenSource? _timeoutSource;
        private readonly CancellationTokenRegistration _timeoutRegistration;
        private readonly CancellationTokenRegistration _cancelRegistration;
        private volatile bool _timedOut;

        public RunningProcess(Process process, ProcessOptions options)
        {
            _process = process;
            _options = options;

            // Timers start with the process, not with WaitAsync, so a timeout counts from launch.
            if (options.Timeout is { } timeout)
            {
                _timeoutSource = new CancellationTokenSource(timeout);
                _timeoutRegistration = _timeoutSource.Token.Register(OnTimeout);
            }

            if (options.Cancellation.CanBeCanceled)
            {
                _cancelRegistration = options.Cancellation.Register(KillTree);
            }

            _stdoutReaderTask = ReadStdoutLoopAsync();
            _stderrReaderTask = options.InheritConsole ? Task.CompletedTask : ReadStderrLoopAsync();
        }

        public IAsyncEnumerable<string> StdoutLines => ReadStdoutLinesAsync();

        public string StderrTail => _stderr.ReadTail();

        public bool TimedOut => _timedOut;

        public int ExitCode
        {
            get
            {
                if (!_process.HasExited)
                {
                    return -1;
                }
                return _process.ExitCode;
            }
        }

        private async IAsyncEnumerable<string> ReadStdoutLinesAsync()
        {
            await foreach (var line in _stdoutChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                yield return line;
            }
        }

        private async Task ReadStdoutLoopAsync()
        {
            try
            {
                if (_options.InheritConsole || _process.StandardOutput is null)
                {
                    return;
                }

                while (true)
                {
                    var line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }
                    await _stdoutChannel.Writer.WriteAsync(line).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _stdoutChannel.Writer.Complete();
            }
        }

        private async Task ReadStderrLoopAsync()
        {
            try
            {
                var buffer = new byte[8192];
                int read;
                while ((read = await _process.StandardError.BaseStream.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
                {
                    _stderr.Write(buffer.AsSpan(0, read));
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public async Task<int> WaitAsync(CancellationToken ct)
        {
            try
            {
                await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillTree();
                await WaitForKilledAsync().ConfigureAwait(false);
                throw;
            }

            // A grandchild can keep a pipe open after the child exits; never wait on it forever.
            await Task.WhenAny(Task.WhenAll(_stdoutReaderTask, _stderrReaderTask), Task.Delay(DrainLimit, CancellationToken.None)).ConfigureAwait(false);

            return _timedOut ? -1 : _process.ExitCode;
        }

        /// <summary>Windows updates HasExited a moment after TerminateProcess; wait (bounded) so callers see the exit.</summary>
        private async Task WaitForKilledAsync()
        {
            using var bound = new CancellationTokenSource(KillWait);
            try
            {
                await _process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnTimeout()
        {
            if (_process.HasExited)
            {
                return;
            }

            _timedOut = true;
            KillTree();
        }

        private void KillTree()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                KillParent();
            }
            catch (Win32Exception)
            {
                // Windows can fail while walking the child tree; the parent must still die.
                KillParent();
            }
        }

        private void KillParent()
        {
            try
            {
                _process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Already exited, so there is nothing left to kill.
            }
            catch (Win32Exception)
            {
                // The OS refused the signal because the process already exited.
            }
        }

        public ValueTask DisposeAsync()
        {
            _timeoutRegistration.Dispose();
            _cancelRegistration.Dispose();
            _timeoutSource?.Dispose();

            try
            {
                if (!_process.HasExited)
                {
                    KillTree();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
