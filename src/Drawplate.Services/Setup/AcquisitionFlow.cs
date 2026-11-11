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
                _stdout.WriteLine("FFmpeg is already installed.");
                return Exit(0);
            }

            return Found(candidate!);
        }

        // An explicit path or environment override is authoritative: do not silently install
        // another FFmpeg when the user's selected executable is broken.
        if (candidate is not null
            && candidate.Source is FfmpegSource.Argument or FfmpegSource.Environment)
        {
            WriteInfoFailure(candidate.Path, info!);
            return Exit(3);
        }

        if (neverInstall)
        {
            if (candidate is null)
            {
                WriteNotFound();
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
            WriteInstallFailure(result.ExitCode ?? -1);
            return Exit(3);
        }

        // Package managers can install into a known folder without changing this process's PATH.
        var candidate = Locate(ffmpegPath);
        if (candidate is null)
        {
            WriteNotFoundAfterInstall();
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
            _stdout.WriteLine("FFmpeg installed.");
            return Exit(0);
        }

        _stdout.WriteLine("FFmpeg installed.");
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

    private void WriteNotFound() =>
        _stderr.WriteLine(InstallAdvisor.NotFoundText(_platform, InstallAdvisor.Suggestions(_platform)));

    private void WriteInfoFailure(string path, FfmpegInfoResult result)
    {
        if (result.TooOld is { } tooOld)
        {
            var message = Messages.E_FF_07(path, tooOld.Version.ToString());
            WriteMessage(message);
            return;
        }

        WriteMessage(Messages.E_FF_02(path, result.NotRunnable?.Reason ?? "unknown"));
    }

    private void WriteMessage(Message message)
    {
        _stderr.WriteLine(message.Text);
        if (message.Hint is { } hint)
        {
            _stderr.WriteLine($"  {hint}");
        }
    }

    private void WriteNotFoundAfterInstall()
    {
        _stderr.WriteLine("FFmpeg was installed, but Drawplate cannot find it yet.");
        _stderr.WriteLine("  Open a new terminal and run Drawplate again. Run 'drawplate doctor' to see where it looks.");
    }

    private void WriteInstallFailure(int code)
    {
        _stderr.WriteLine($"The install command failed (exit code {code}).");
        _stderr.WriteLine("  See \"Troubleshooting FFmpeg\" in the README.");
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
