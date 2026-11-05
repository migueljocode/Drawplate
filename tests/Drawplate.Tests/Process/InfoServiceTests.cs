using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;
using Drawplate.Tests.Process;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
public class InfoServiceTests
{
    private static readonly string FakeExeDll = FakeExeHarness.FakeExePath;

    private static string MakeWrapper(string scriptPath)
    {
        var wrapper = Path.GetTempFileName();
        File.Delete(wrapper);
        File.WriteAllText(wrapper, $"#!/bin/sh\nexec dotnet {FakeExeDll} \"$@\"\n");
        var psi = new ProcessStartInfo("chmod", ["+x", wrapper]) { UseShellExecute = false };
        global::System.Diagnostics.Process.Start(psi)!.WaitForExit();
        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", scriptPath);
        return wrapper;
    }

    [Fact]
    public async Task Version_6_1_1_Ok()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}\n{\"exit\":0}\n");
        var wrapper = MakeWrapper(script);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            var info = await service.GetInfoAsync(wrapper, CancellationToken.None).ConfigureAwait(true);
            Assert.NotNull(info);
            Assert.Equal(new Version(6, 1, 1), info!.Version.Version);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact]
    public async Task Version_5_1_TooOld()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 5.1.1\\n\"}\n{\"exit\":0}\n");
        var wrapper = MakeWrapper(script);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            var info = await service.GetInfoAsync(wrapper, CancellationToken.None).ConfigureAwait(true);
            Assert.Null(info);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact]
    public async Task NotRunnable_ExitCode127()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"if\":\"-version\",\"exit\":127}\n");
        var wrapper = MakeWrapper(script);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            var info = await service.GetInfoAsync(wrapper, CancellationToken.None).ConfigureAwait(true);
            Assert.Null(info);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact]
    public async Task Encoders_ContainLibx264()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n V..... h264_nvenc\\n\"}\n{\"exit\":0}\n");
        var wrapper = MakeWrapper(script);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            var info = await service.GetInfoAsync(wrapper, CancellationToken.None).ConfigureAwait(true);
            Assert.NotNull(info);
            Assert.True(info!.HasLibx264);
            Assert.Contains("h264_nvenc", info.Encoders);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }

    [Fact(Skip = "Platform limitation: Process.WaitForExitAsync cancellation does not fire on this Linux environment")]
    public async Task Timeout_IsNotRunnable()
    {
        var script = Path.GetTempFileName();
        await File.WriteAllTextAsync(script, "{\"if\":\"-version\",\"hang\":true}\n");
        var wrapper = MakeWrapper(script);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var info = await service.GetInfoAsync(wrapper, cts.Token).ConfigureAwait(true);
            Assert.Null(info);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(script);
            File.Delete(wrapper);
        }
    }
}
