using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Drawplate.Services.Processes;

public interface IRunningProcess : IAsyncDisposable
{
    IAsyncEnumerable<string> StdoutLines { get; }
    string StderrTail { get; }
    bool TimedOut { get; }
    int ExitCode { get; }
    Task<int> WaitAsync(CancellationToken ct);
}
