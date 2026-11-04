using System;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;

namespace Drawplate.Models.Planning;

public static class ConversionPlanner
{
    public static ConversionPlan Build(
        ImmutableArray<string> sources,
        string outputFolder,
        IPlanFileSystem fs,
        OperatingSystemKind os)
    {
        ArgumentNullException.ThrowIfNull(outputFolder);
        ArgumentNullException.ThrowIfNull(fs);

        var outputFull = fs.FullPath(outputFolder);
        var files = ImmutableArray.CreateBuilder<PlannedFile>();

        foreach (var source in sources)
        {
            var status = Classify(source, outputFull, fs, os, out var reason);
            files.Add(new PlannedFile
            {
                Source = source,
                TargetName = OutputNaming.TargetName(Path.GetFileName(source)),
                PartialName = OutputNaming.PartialName(Path.GetFileName(source), Environment.ProcessId),
                Status = status,
                SkipReason = reason,
            });
        }

        var collisions = FindCollisions(files.ToImmutable(), os);

        return new ConversionPlan
        {
            Files = files.ToImmutable(),
            Collisions = collisions,
        };
    }

    private static PlanStatus Classify(
        string source,
        string outputFull,
        IPlanFileSystem fs,
        OperatingSystemKind os,
        out string? reason)
    {
        reason = null;

        if (!fs.FileExists(source) && !fs.DirectoryExists(source))
        {
            reason = "File not found: " + source;
            return PlanStatus.Missing;
        }

        if (fs.DirectoryExists(source))
        {
            reason = "Not a file: " + source;
            return PlanStatus.NotAFile;
        }

        if (!InputRules.IsSupported(source))
        {
            reason = source + " (unsupported file type)";
            return PlanStatus.SkipUnsupported;
        }

        var resolvedSource = fs.ResolveLinks(source);
        if (IsInsideOutput(resolvedSource, outputFull, os))
        {
            reason = "already in the output folder";
            return PlanStatus.SkipInsideOutput;
        }

        var target = Path.Combine(outputFull, OutputNaming.TargetName(Path.GetFileName(source)));
        if (fs.FileExists(target))
        {
            reason = "output already exists";
            return PlanStatus.SkipExists;
        }

        return PlanStatus.Convert;
    }

    private static bool IsInsideOutput(string source, string outputFull, OperatingSystemKind os)
    {
        var comparison = os is OperatingSystemKind.Linux
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (string.Equals(source, outputFull, comparison))
        {
            return true;
        }

        var outputWithSeparator = outputFull.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? outputFull
            : outputFull + System.IO.Path.DirectorySeparatorChar;

        return source.StartsWith(outputWithSeparator, comparison);
    }

    private static ImmutableArray<NameCollision> FindCollisions(
        ImmutableArray<PlannedFile> files,
        OperatingSystemKind os)
    {
        var comparison = os is OperatingSystemKind.Linux
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.OrdinalIgnoreCase;

        var groups = files
            .Where(f => f.Status == PlanStatus.Convert)
            .GroupBy(f => f.TargetName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .Select(g => new NameCollision
            {
                OutputName = g.Key!,
                Sources = g.Select(f => f.Source).ToImmutableArray(),
            })
            .ToImmutableArray();

        return groups;
    }
}
