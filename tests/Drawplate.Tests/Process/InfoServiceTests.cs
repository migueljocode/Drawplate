using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Processes;

using Xunit;

namespace Drawplate.Tests.Process;

[Collection("FakeExe")]
public class InfoServiceTests
{
    private static readonly string FakeExeDll = FakeExeHarness.FakeExePath;

    /// <summary>A launcher script that runs FakeExe: a shell script on Unix, a .cmd file on Windows.</summary>
    private static string MakeWrapper(string scriptPath)
    {
        var wrapper = Path.GetTempFileName();
        File.Delete(wrapper);
        if (OperatingSystem.IsWindows())
        {
            wrapper = Path.ChangeExtension(wrapper, ".cmd");
            File.WriteAllText(wrapper, $"@echo off\r\ndotnet \"{FakeExeDll}\" %*\r\n");
        }
        else
        {
            File.WriteAllText(wrapper, $"#!/bin/sh\nexec dotnet {FakeExeDll} \"$@\"\n");
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", scriptPath);
        return wrapper;
    }

    private static async Task<FfmpegInfoResult> InfoForAsync(string script)
    {
        var scriptPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(scriptPath, script).ConfigureAwait(false);
        var wrapper = MakeWrapper(scriptPath);
        try
        {
            var service = new FfmpegInfoService(new ProcessLauncher());
            return await service.GetInfoAsync(wrapper, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(scriptPath);
            File.Delete(wrapper);
        }
    }

    [Fact]
    public async Task Version_6_1_1_Ok()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        Assert.NotNull(result.Info);
        Assert.Equal(new Version(6, 1, 1), result.Info!.Version.Version);
    }

    [Fact]
    public async Task Version_5_1_TooOld()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"stdout\":\"ffmpeg version 5.1.1\\n\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        Assert.Null(result.Info);
        Assert.NotNull(result.TooOld);
        Assert.Equal(new Version(5, 1, 1), result.TooOld!.Version);
    }

    [Fact]
    public async Task InfoService_MasterBuild_NotTooOld()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"stdout\":\"ffmpeg version N-113456-gabcdef1234 Copyright (c) 2000-2026\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        Assert.Null(result.TooOld);
        Assert.NotNull(result.Info);
    }

    [Fact]
    public async Task NotRunnable_ExitCode127()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"exit\":127}\n").ConfigureAwait(true);
        Assert.Null(result.Info);
        Assert.NotNull(result.NotRunnable);
        Assert.StartsWith("exit code", result.NotRunnable!.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InfoService_UnreadableVersion_ReportsUnreadable()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"stdout\":\"hello world\\n\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        Assert.Equal("unreadable version output", result.NotRunnable!.Reason);
    }

    [Fact]
    public async Task Encoders_ContainLibx264()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"stdout\":\"ffmpeg version 6.1.1\\n\"}\n{\"if\":\"-encoders\",\"stdout\":\" V..... libx264\\n V..... h264_nvenc\\n\"}\n{\"exit\":0}\n").ConfigureAwait(true);
        Assert.NotNull(result.Info);
        Assert.True(result.Info!.HasLibx264);
        Assert.Contains("h264_nvenc", result.Info.Encoders);
    }

    [Fact]
    public async Task InfoService_MissingFile_ReportsFileNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-ffmpeg-" + Guid.NewGuid().ToString("N"));
        var result = await new FfmpegInfoService(new ProcessLauncher()).GetInfoAsync(missing, CancellationToken.None).ConfigureAwait(true);
        Assert.Equal("file not found", result.NotRunnable!.Reason);
    }

    [Fact]
    public async Task InfoService_StartFails_ReportsCannotBeStarted()
    {
        // An existing file that is not an executable program (no execute bit on Unix, not a valid program on Windows).
        var file = Path.GetTempFileName();
        try
        {
            var result = await new FfmpegInfoService(new ProcessLauncher()).GetInfoAsync(file, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal("cannot be started", result.NotRunnable!.Reason);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task InfoService_UserCancel_Propagates()
    {
        var scriptPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(scriptPath, "{\"if\":\"-version\",\"hang\":true}\n").ConfigureAwait(true);
        var wrapper = MakeWrapper(scriptPath);
        try
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            var service = new FfmpegInfoService(new ProcessLauncher());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetInfoAsync(wrapper, cts.Token)).ConfigureAwait(true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_EXE_SCRIPT", null);
            File.Delete(scriptPath);
            File.Delete(wrapper);
        }
    }

    [Fact(Skip = "Takes the full 10 s command timeout; the launcher timeout itself is covered by LauncherTests")]
    public async Task Timeout_IsNotRunnable()
    {
        var result = await InfoForAsync("{\"if\":\"-version\",\"hang\":true}\n").ConfigureAwait(true);
        Assert.Equal("timed out", result.NotRunnable!.Reason);
    }
}
