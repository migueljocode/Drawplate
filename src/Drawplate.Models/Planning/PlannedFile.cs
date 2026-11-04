namespace Drawplate.Models.Planning;

public enum PlanStatus
{
    Missing,
    NotAFile,
    SkipUnsupported,
    SkipInsideOutput,
    SkipExists,
    Convert
}

public sealed record PlannedFile
{
    public required string Source { get; init; }
    public required string TargetName { get; init; }
    public required string PartialName { get; init; }
    public required PlanStatus Status { get; init; }
    public required string? SkipReason { get; init; }
}

public sealed record NameCollision
{
    public required string OutputName { get; init; }
    public required ImmutableArray<string> Sources { get; init; }
}

public sealed record ConversionPlan
{
    public required ImmutableArray<PlannedFile> Files { get; init; }
    public required ImmutableArray<NameCollision> Collisions { get; init; }
}
