using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drawplate.Services.Processes;

public sealed class ProcessLauncher : IProcessLauncher
{
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
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        if (opts.InheritConsole)
        {
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;
        }
        else
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
        }

        var process = Process.Start(psi)!;
        return new RunningProcess(process, opts);
    }

    private sealed class RunningProcess : IRunningProcess
    {
        private readonly Process _process;
        private readonly ProcessOptions _options;
        private readonly RingBuffer _stderr = new(65536);
        private readonly Channel<string> _stdoutChannel = Channel.CreateUnbounded<string>();
        private Task? _stdoutReaderTask;
        private Task? _stderrReaderTask;
        private int _exitCode = -1;
        private bool _timedOut;

        public RunningProcess(Process process, ProcessOptions options)
        {
            _process = process;
            _options = options;

            _stdoutReaderTask = ReadStdoutLoopAsync();
            if (!options.InheritConsole)
            {
                _stderrReaderTask = ReadStderrLoopAsync();
            }
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
                    _stdoutChannel.Writer.Complete();
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var waitTask = _process.WaitForExitAsync(cts.Token);

            if (_options.Timeout is { } timeout)
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var completed = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);
                if (completed == delayTask)
                {
                    _timedOut = true;
                    KillTree();
                    try
                    {
                        await waitTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return -1;
                }
            }

            cts.Token.ThrowIfCancellationRequested();

            try
            {
                await waitTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillTree();
                try
                {
                    await Task.Delay(5000, CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                throw;
            }

            try
            {
                await _stdoutReaderTask!.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _exitCode = _process.ExitCode;
            return _exitCode;
        }

        private void KillTree()
        {
            try
            {
                _process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            KillChildProcesses(_process.Id);
        }

        private static void KillChildProcesses(int parentPid)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                KillChildProcessesLinux(parentPid);
            }
        }

        private static void KillChildProcessesLinux(int parentPid)
        {
            try
            {
                foreach (var pidDir in Directory.GetDirectories("/proc"))
                {
                    if (!int.TryParse(Path.GetFileName(pidDir), out var pid))
                    {
                        continue;
                    }

                    try
                    {
                        var stat = File.ReadAllText(Path.Combine(pidDir, "stat"));
                        var parts = stat.Split(' ');
                        if (parts.Length >= 4 && int.TryParse(parts[3], out var ppid) && ppid == parentPid)
                        {
                            try
                            {
                                Process.GetProcessById(pid).Kill();
                            }
                            catch (InvalidOperationException)
                            {
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                            }
                            KillChildProcessesLinux(pid);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public ValueTask DisposeAsync()
        {
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
            catch (System.ComponentModel.Win32Exception)
            {
            }

            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
