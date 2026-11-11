using System.Globalization;
using System.Linq;

namespace Drawplate.Models.Cli;

using Drawplate.Models.Encoding;
using Drawplate.Models.Planning;

public static class Messages
{
    public static Message E_USAGE_01_Long(string arg) =>
        new("E-USAGE-01", $"Unknown option '{arg}'.", "Run 'drawplate --help' to see all options.", 2);

    public static Message E_USAGE_01_Short(char c) =>
        new("E-USAGE-01", $"Unknown option -{c}.", "Run 'drawplate --help' to see all options.", 2);

    public static Message E_USAGE_02(string arg) =>
        new("E-USAGE-02", $"Option {arg} requires a value.", null, 2);

    public static Message E_USAGE_03(string name) =>
        new("E-USAGE-03", $"Option --{name} does not take a value.", "Run 'drawplate --help' to see all options.", 2);

    public static Message E_USAGE_04(string value, string list) =>
        new("E-USAGE-04", $"Unknown encoder '{value}'.", $"Choose one of: {list}.", 2);

    public static Message E_USAGE_05() =>
        new("E-USAGE-05", "Quality must be a number from 1 to 100 (higher = better).", null, 2);

    public static Message E_USAGE_06(string value) =>
        new("E-USAGE-06", $"Invalid max size '{value}'.", "Choose one of: sd, hd, fullhd, 4k.", 2);

    public static Message E_USAGE_07(string path) =>
        new("E-USAGE-07", $"Input folder not found: {path}", null, 2);

    public static Message E_USAGE_08(string path) =>
        new("E-USAGE-08", $"Output path is a file, not a folder: {path}", null, 2);

    public static Message E_USAGE_09(ImmutableArray<NameCollision> groups)
    {
        var lines = string.Join("\n", groups.Select(g => $"  {g.OutputName}: {string.Join(", ", g.Sources)}"));
        return new("E-USAGE-09",
            $"More than one input would write the same output file:\n{lines}",
            "Convert them in separate runs or rename one.", 2);
    }

    public static Message E_USAGE_10() =>
        new("E-USAGE-10", "Interactive mode needs a terminal.", null, 2);

    public static Message E_USAGE_11() =>
        new("E-USAGE-11", "Use either -i <folder> or file names, not both.", null, 2);

    public static Message E_USAGE_12(string value, string os, string list) =>
        new("E-USAGE-12", $"Encoder '{value}' is not available on {os}.", $"Choose one of: {list}.", 2);

    public static Message E_RUN_01() =>
        new("E-RUN-01", "No supported video files found.", "Supported formats: mov, mp4, mkv, avi, webm.", 1);

    public static Message E_RUN_02(string value) =>
        new("E-RUN-02", $"Encoder '{value}' is not working on this machine.",
            "Run 'drawplate --list-encoders' to see what works.", 1);

    public static Message W_01(string path) =>
        new("W-01", $"File not found: {path}", null, 0);

    public static Message W_02(string path) =>
        new("W-02", $"Not a file: {path}", null, 0);

    public static string N_01(string name) => $"{name} (unsupported file type)";

    public static Message N_INT() =>
        new("N-INT", "Interrupted.", null, 130);

    public static string N_VERSION(string version) => $"drawplate {version}";

    public static Message E_FF_02(string path, string reason) =>
        new("E-FF-02", $"FFmpeg at {path} could not be run ({reason}).", "Reinstall it, or choose another with --ffmpeg-path.", 3);

    public static Message E_FF_03(OperatingSystemKind os) =>
        new("E-FF-03", "This FFmpeg build has no libx264 encoder and no working hardware encoder, so nothing can encode.", os switch
        {
            OperatingSystemKind.Windows => "Install the full build: winget install --id Gyan.FFmpeg -e",
            OperatingSystemKind.MacOS => "Install it with: brew install ffmpeg",
            _ => "See \"Troubleshooting FFmpeg\" in the README."
        }, 3);

    public static Message E_FF_07(string version, OperatingSystemKind os) =>
        new("E-FF-07", $"FFmpeg {version} is older than the supported minimum 6.0.", os switch
        {
            OperatingSystemKind.Windows => "Upgrade it: winget upgrade --id Gyan.FFmpeg -e",
            OperatingSystemKind.MacOS => "brew upgrade ffmpeg",
            _ => "Update it with your package manager."
        }, 3);
}
