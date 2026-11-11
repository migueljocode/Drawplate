using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Encoding;
using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Setup;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Setup;

public sealed partial class AcquisitionFlowTests
{
    [Fact]
    [Trait("Tier", "T1")]
    public async Task Acquisition_AfterInstall_RelocatesInKnownFolders()
    {
        var script = FakeExeHarness.WriteScript([
            "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}",
            "{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}",
            "{\"exit\":0}"
        ]);
        var wrapper = FakeExeHarness.CreateWrapper();
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", script);
        try
        {
            var candidate = new FfmpegCandidate(wrapper, FfmpegSource.KnownFolder);
            var locateCalls = 0;
            var locator = new Func<string?, FfmpegCandidate?>(_ => ++locateCalls == 1 ? null : candidate);
            var prompt = new TestPrompt(true, true);
            var launcher = new StubLauncher(0);
            var (flow, stdout, _) = CreateFlow(
                prompt,
                null,
                OperatingSystemKind.Windows,
                locator,
                launcher,
                ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "winget"));

            var outcome = await flow.RunAsync(AcquisitionMode.SetupCommand, false, false, null, CancellationToken.None);

            Assert.Equal(AcquisitionStatus.Exit, outcome.Status);
            Assert.Equal(0, outcome.ExitCode);
            Assert.Equal(2, locateCalls);
            Assert.Equal("FFmpeg installed." + Environment.NewLine, stdout.ToString());
            Assert.Equal("winget", launcher.Executable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }
}
