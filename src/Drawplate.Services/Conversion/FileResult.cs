using System;

namespace Drawplate.Services.Conversion;

public enum FileResultStatus
{
    Converted,
    Failed,
    Skipped
}

public sealed record FileResult
{
    public required FileResultStatus Status { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required string? Encoder { get; init; }
    public required string? Reason { get; init; }
    public required long InputSize { get; init; }
    public required long OutputSize { get; init; }
    public required TimeSpan Elapsed { get; init; }
}

public delegate void FileProgressHandler(string source, double? fraction, double? speed, TimeSpan elapsed);
