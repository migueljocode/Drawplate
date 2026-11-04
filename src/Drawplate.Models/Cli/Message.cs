namespace Drawplate.Models.Cli;

public sealed record Message(string Id, string Text, string? Hint, int ExitCode);
