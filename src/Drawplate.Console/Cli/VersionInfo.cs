using System.Reflection;

namespace Drawplate.Console.Cli;

internal static class VersionInfo
{
    internal static string Current { get; } = FromInformational(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>Strips the source-link commit suffix (+abc123); 0.0.0 when no version is embedded.</summary>
    internal static string FromInformational(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
