using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Drawplate.Tests.Integration;

internal static class MediaFixtures
{
    public static string? FindFfmpeg()
    {
        var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static string FfmpegPath => FindFfmpeg() ?? throw new InvalidOperationException("FFmpeg is required.");

    public static string DrawplatePath
    {
        get
        {
            var dir = Path.GetDirectoryName(typeof(MediaFixtures).Assembly.Location)!;
            var root = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
            var exe = OperatingSystem.IsWindows() ? "drawplate.exe" : "drawplate";
            foreach (var config in new[] { "Release", "Debug" })
            {
                var path = Path.Combine(root, "src", "Drawplate.Console", "bin", config, "net10.0", exe);
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("Build Drawplate.Console first.");
        }
    }

    public static string Temp()
    {
        var path = Path.Combine(Path.GetTempPath(), "drawplate-r16-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static async Task Clip(string folder, string name, int width = 320, int height = 240, bool audio = true,
        string videoCodec = "libx264", string audioCodec = "aac")
    {
        var args = new List<string> { "-y", "-loglevel", "error", "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:rate=24:duration=1" };
        if (audio) args.AddRange(["-f", "lavfi", "-i", "sine=frequency=440:duration=1"]);
        args.AddRange(["-c:v", videoCodec]);
        if (videoCodec == "libvpx") args.AddRange(["-b:v", "200k"]);
        if (audio) args.AddRange(["-c:a", audioCodec, "-shortest"]);
        args.Add(Path.Combine(folder, name));
        await Run(FfmpegPath, args, folder).ConfigureAwait(false);
    }

    public static async Task<Command> RunCli(string cwd, params string[] args) =>
        await Run(DrawplatePath, args, cwd).ConfigureAwait(false);

    public static async Task<Command> Run(string exe, IEnumerable<string> args, string cwd, IDictionary<string, string?>? env = null)
    {
        var psi = new ProcessStartInfo { FileName = exe, WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        if (env is not null) foreach (var item in env) psi.Environment[item.Key] = item.Value;
        using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Could not start process.");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { p.Kill(entireProcessTree: true); throw new TimeoutException("Process exceeded 60 seconds."); }
        return new Command(p.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    internal readonly record struct Command(int ExitCode, string Stdout, string Stderr);
}
