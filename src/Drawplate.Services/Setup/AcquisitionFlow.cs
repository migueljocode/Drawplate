using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Cli;
using Drawplate.Models.Encoding;
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

    public AcquisitionFlow(
        FfmpegInfoService infoService,
        IPrompt prompt,
        InstallRunner installRunner,
        PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(infoService);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(installRunner);
        ArgumentNullException.ThrowIfNull(platform);
        _infoService = infoService;
        _prompt = prompt;
        _installRunner = installRunner;
        _platform = platform;
    }

    public async Task<AcquisitionOutcome> RunAsync(
        AcquisitionMode mode,
        bool assumeYes,
        bool neverInstall,
        string? ffmpegPath,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);

        var pathVar = Environment.GetEnvironmentVariable("DRAWPLATE_FFMPEG");
        var candidate = FfmpegLocator.Locate(
            ffmpegPath,
            pathVar,
            PathFolders.Parse(Environment.GetEnvironmentVariable("PATH")),
            File.Exists,
            FfmpegLocator.EnumerateDirectories,
            KnownFolders.For(_platform.Os, Environment.GetEnvironmentVariable),
            _platform.Os);

        // Step 1: -n / never install
        if (neverInstall || mode == AcquisitionMode.SetupCommand && candidate is null)
        {
            if (candidate is null)
            {
                var suggestions = InstallAdvisor.Suggestions(_platform);
                WriteNotFound(suggestions);
                return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
            }
        }

        // Candidate found and usable
        if (candidate is not null)
        {
            var info = await _infoService.GetInfoAsync(candidate.Path, ct).ConfigureAwait(false);
            if (info.Info is not null)
            {
                if (mode == AcquisitionMode.SetupCommand)
                {
                    WriteN_SETUP_01();
                    return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 0 };
                }
                return new AcquisitionOutcome { Status = AcquisitionStatus.Found, Candidate = candidate, ExitCode = 0 };
            }
        }

        // FFmpeg missing or unusable
        if (mode == AcquisitionMode.SetupCommand)
        {
            // setup runs flow even when present; already handled above if usable
        }

        // -n: no install
        if (neverInstall)
        {
            var suggestions = InstallAdvisor.Suggestions(_platform);
            WriteNotFound(suggestions);
            return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
        }

        // -y: assume yes
        if (assumeYes)
        {
            var suggestions = InstallAdvisor.Suggestions(_platform);
            var runnable = suggestions.FirstOrDefault(s => s.RunnableByApp);
            if (runnable is not null)
            {
                var result = await _installRunner.RunAsync(runnable, ct).ConfigureAwait(false);
                if (result.ExitCode == 0)
                {
                    var info2 = await _infoService.GetInfoAsync(candidate?.Path ?? ffmpegPath, ct).ConfigureAwait(false);
                    if (info2.Info is not null)
                    {
                        WriteN_OK_01();
                        return new AcquisitionOutcome { Status = AcquisitionStatus.Found, Candidate = candidate, ExitCode = 0 };
                    }
                }
                WriteE_FF_08(result.ExitCode ?? -1);
                return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
            }
            // none runnable
            var suggestions2 = InstallAdvisor.Suggestions(_platform);
            WriteNotFound(suggestions2);
            return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
        }

        // Interactive terminal
        if (_prompt.IsInteractive)
        {
            var suggestions = InstallAdvisor.Suggestions(_platform);
            WriteNotFound(suggestions);
            var runnable = suggestions.FirstOrDefault(s => s.RunnableByApp);
            if (runnable is not null)
            {
                var confirmed = _prompt.Confirm("Run it now?", true);
                if (confirmed)
                {
                    var result = await _installRunner.RunAsync(runnable, ct).ConfigureAwait(false);
                    if (result.ExitCode == 0)
                    {
                        var info2 = await _infoService.GetInfoAsync(candidate?.Path ?? ffmpegPath, ct).ConfigureAwait(false);
                        if (info2.Info is not null)
                        {
                            WriteN_OK_01();
                            return new AcquisitionOutcome { Status = AcquisitionStatus.Found, Candidate = candidate, ExitCode = 0 };
                        }
                        WriteE_FF_04();
                        return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
                    }
                    WriteE_FF_08(result.ExitCode ?? -1);
                    return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
                }
                // user said no
                return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
            }
        }

        // Non-interactive
        var suggestions3 = InstallAdvisor.Suggestions(_platform);
        WriteNotFound(suggestions3);
        return new AcquisitionOutcome { Status = AcquisitionStatus.Exit, ExitCode = 3 };
    }

    private static void WriteNotFound(ImmutableArray<InstallSuggestion> suggestions)
    {
        // Build platform-specific text - simplified
        var text = "FFmpeg was not found.\n\nInstall it with your package manager:\n";
        foreach (var s in suggestions)
        {
            foreach (var line in s.DisplayLines)
            {
                text += line + "\n";
            }
        }
        global::System.Console.Error.WriteLine(text);
    }

    private static void WriteN_SETUP_01()
    {
        global::System.Console.WriteLine("FFmpeg is already installed.");
    }

    private static void WriteN_OK_01()
    {
        global::System.Console.WriteLine("FFmpeg installed.");
    }

    private static void WriteE_FF_04()
    {
        global::System.Console.Error.WriteLine("FFmpeg was installed, but Drawplate cannot find it yet.");
        global::System.Console.Error.WriteLine("  Open a new terminal and run Drawplate again. Run 'drawplate doctor' to see where it looks.");
    }

    private static void WriteE_FF_08(int code)
    {
        global::System.Console.Error.WriteLine($"The install command failed (exit code {code}).");
        global::System.Console.Error.WriteLine("  See \"Troubleshooting FFmpeg\" in the README.");
    }
}

public enum AcquisitionMode
{
    Convert,
    SetupCommand,
}
