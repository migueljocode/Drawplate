using System.Collections.Generic;

namespace Drawplate.Services.Processes;

public interface IProcessLauncher
{
    IRunningProcess Start(string executable, IReadOnlyList<string> arguments, ProcessOptions? options = null);
}
