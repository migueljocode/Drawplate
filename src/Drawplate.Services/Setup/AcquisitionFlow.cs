using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Cli;
using Drawplate.Models.Setup;
using Drawplate.Services.Ffmpeg;
using Drawplate.Services.Interaction;
using Drawplate.Services.Platform;
using Drawplate.Services.Processes;

namespace Drawplate.Services.Setup;

public sealed record AcquisitionOutcome
{
    public required AcquisitionStatus Status { get; init; }
    public FfmpegCandidate? Candidate { get; init; }
    public required int ExitCode { get; init; }
}

public enum AcquisitionStatus
{
    Found,
    Exit,
}

public sealed class AcquisitionFlow
{
    private readonly FfmpegInfoService _infoService;
    private readonly IPrompt _prompt;
    private readonly InstallRunner _installRunner;
    private readonly PlatformInfo _platform;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public AcquisitionFlow(
        FfmpegInfoService infoService,
        IPrompt prompt,
        InstallRunner installRunner,
        PlatformInfo platform,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(infoService);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(installRunner);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        _infoService = infoService;
        _prompt = prompt;
        _installRunner = installRunner;
        _platform = platform;
        _stdout = stdout;
        _stderr = stderr;
    }

    public async Task<AcquisitionOutcome> RunAsync(
        AcquisitionMode mode,
        bool assumeYes,
        bool neverInstall,
        string? ffmpegPath,
        CancellationToken ct)
    {
        var candidate = Locate(ffmpegPath);
        var info = candidate is null
            ? null
            : await _infoService.GetInfoAsync(candidate.Path, ct).ConfigureAwait(false);

        if (info?.Info is not null)
        {
            if (mode == AcquisitionMode.SetupCommand)
            {
                await _stdout.WriteLineAsync("FFmpeg is already installed.").ConfigureAwait(false);
                return Exit(0);
            }

            return Found(candidate!);
        }

        // An explicit path or environment override is authoritative: do not silently install
        // another FFmpeg when the user's selected executable is broken.
        if (candidate is not null
            && candidate.Source is FfmpegSource.Argument or FfmpegSource.Environment)
        {
            await WriteInfoFailureAsync(candidate.Path, info!).ConfigureAwait(false);
            return Exit(3);
        }

        if (neverInstall)
        {
            if (candidate is null)
            {
                await WriteNotFoundAsync().ConfigureAwait(false);
            }
            else
            {
                WriteInfoFailure(candidate.Path, info!);
            }
            return Exit(3);
        }

        var suggestions = InstallAdvisor.Suggestions(_platform);
        var runnable = suggestions.FirstOrDefault(s => s.RunnableByApp);

        if (assumeYes)
        {
            if (runnable is null)
            {
                WriteNotFound();
                return Exit(3);
            }

            return await InstallAndRelocateAsync(runnable, mode, ffmpegPath, ct).ConfigureAwait(false);
        }

        if (_prompt.IsInteractive)
        {
            WriteNotFound();
            if (runnable is null || !_prompt.Confirm("Run it now?", true))
            {
                return Exit(3);
            }

            return await InstallAndRelocateAsync(runnable, mode, ffmpegPath, ct).ConfigureAwait(false);
        }

        WriteNotFound();
        return Exit(3);
    }

    private async Task<AcquisitionOutcome> InstallAndRelocateAsync(
        InstallSuggestion suggestion,
        AcquisitionMode mode,
        string? ffmpegPath,
        CancellationToken ct)
    {
        var result = await _installRunner.RunAsync(suggestion, ct).ConfigureAwait(false);
        if (!result.Started || result.ExitCode != 0)
        {
            await WriteInstallFailureAsync(result.ExitCode ?? -1).ConfigureAwait(false);
            return Exit(3);
        }

        // Package managers can install into a known folder without changing this process's PATH.
        var candidate = Locate(ffmpegPath);
        if (candidate is null)
        {
            await WriteNotFoundAfterInstallAsync().ConfigureAwait(false);
            return Exit(3);
        }

        var info = await _infoService.GetInfoAsync(candidate.Path, ct).ConfigureAwait(false);
        if (info.Info is null)
        {
            WriteNotFoundAfterInstall();
            return Exit(3);
        }

        if (mode == AcquisitionMode.SetupCommand)
        {
            await _stdout.WriteLineAsync("FFmpeg installed.").ConfigureAwait(false);
            return Exit(0);
        }

        await _stdout.WriteLineAsync("FFmpeg installed.").ConfigureAwait(false);
        return Found(candidate);
    }

    private FfmpegCandidate? Locate(string? ffmpegPath)
    {
        var os = _platform.Os;
        return FfmpegLocator.Locate(
            ffmpegPath,
            Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG"),
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(os, Environment.GetEnvironmentVariable),
            os);
    }

    private Task WriteNotFoundAsync() =>
        _stderr.WriteLineAsync(InstallAdvisor.NotFoundText(_platform, InstallAdvisor.Suggestions(_platform)));

    private async Task WriteInfoFailureAsync(string path, FfmpegInfoResult result)
    {
        if (result.TooOld is { } tooOld)
        {
            var message = Messages.E_FF_07(path, tooOld.Version.ToString());
            await WriteMessageAsync(message).ConfigureAwait(false);
            return;
        }

        await WriteMessageAsync(Messages.E_FF_02(path, result.NotRunnable?.Reason ?? "unknown")).ConfigureAwait(false);
    }

    private async Task WriteMessageAsync(Message message)
    {
        await _stderr.WriteLineAsync(message.Text).ConfigureAwait(false);
        if (message.Hint is { } hint)
        {
            await _stderr.WriteLineAsync($"  {hint}").ConfigureAwait(false);
        }
    }

    private async Task WriteNotFoundAfterInstallAsync()
    {
        await _stderr.WriteLineAsync("FFmpeg was installed, but Drawplate cannot find it yet.").ConfigureAwait(false);
        await _stderr.WriteLineAsync("  Open a new terminal and run Drawplate again. Run 'drawplate doctor' to see where it looks.").ConfigureAwait(false);
    }

    private async Task WriteInstallFailureAsync(int code)
    {
        await _stderr.WriteLineAsync($"The install command failed (exit code {code}).").ConfigureAwait(false);
        await _stderr.WriteLineAsync("  See \"Troubleshooting FFmpeg\" in the README.").ConfigureAwait(false);
    }

    private static AcquisitionOutcome Found(FfmpegCandidate candidate) =>
        new() { Status = AcquisitionStatus.Found, Candidate = candidate, ExitCode = 0 };

    private static AcquisitionOutcome Exit(int code) =>
        new() { Status = AcquisitionStatus.Exit, ExitCode = code };
}

public enum AcquisitionMode
{
    Convert,
    SetupCommand,
}
