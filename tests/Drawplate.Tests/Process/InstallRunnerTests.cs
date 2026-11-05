using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Setup;
using Drawplate.Services.Processes;
using Drawplate.Services.Setup;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
public class InstallRunnerTests
{
    [Fact]
    public async Task NotRunnable_Throws()
    {
        var suggestion = new InstallSuggestion(
            "apt", ["sudo apt install ffmpeg"], null, [], false);
        var runner = new InstallRunner(new ProcessLauncher());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(suggestion, CancellationToken.None));
    }

    [Fact]
    public async Task ExecutableMissing_NotStarted()
    {
        var suggestion = new InstallSuggestion(
            "nonexistent", ["--version"], "nonexistent", [], true);
        var runner = new InstallRunner(new ProcessLauncher());
        var result = await runner.RunAsync(suggestion, CancellationToken.None);
        Assert.False(result.Started);
    }
}
