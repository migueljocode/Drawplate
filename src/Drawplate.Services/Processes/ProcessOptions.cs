using System;
using System.Threading;

namespace Drawplate.Services.Processes;

public sealed record ProcessOptions(TimeSpan? Timeout = null, bool InheritConsole = false, CancellationToken Cancellation = default);
