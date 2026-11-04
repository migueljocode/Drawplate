using System;

namespace Drawplate.Services.Processes;

public sealed record ProcessOptions(TimeSpan? Timeout = null, bool InheritConsole = false);
