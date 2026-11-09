using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Drawplate.Models.Cli;
using Drawplate.Services.Conversion;

using Xunit;

namespace Drawplate.Tests.Integration;

[Trait("Tier", "T1")]
public class InputDiscoveryTests
{
    [Fact]
    public void Discovery_Folder_FullPathsSortedFilesOnly()
    {
        using var folder = new TempFolder();
        foreach (var name in new[] { "b.mp4", "A.mp4", "c.MOV", "notes.txt", ".a.mp4" })
        {
            File.WriteAllText(Path.Combine(folder.Path, name), string.Empty);
        }
        Directory.CreateDirectory(Path.Combine(folder.Path, "x.mp4"));

        var (files, error) = InputDiscovery.FromFolder(folder.Path);

        Assert.Null(error);
        Assert.Equal<string[]>(
            [Path.Combine(folder.Path, "A.mp4"), Path.Combine(folder.Path, "b.mp4"), Path.Combine(folder.Path, "c.MOV")],
            files.ToArray());
    }

    [Fact]
    public void Discovery_Folder_MissingFolder_E_USAGE_07()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N"));

        var (files, error) = InputDiscovery.FromFolder(missing);

        Assert.Empty(files);
        Assert.Equal("E-USAGE-07", error!.Id);
    }

    [Fact]
    public void Discovery_Hidden_Skipped()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, ".a.mp4"), string.Empty);

        var (files, _) = InputDiscovery.FromFolder(folder.Path);

        Assert.Empty(files);
    }

    [Fact]
    public void Discovery_UppercaseExtension_Matched()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "CLIP.MOV"), string.Empty);

        var (files, _) = InputDiscovery.FromFolder(folder.Path);

        Assert.Single(files);
    }

    /// <summary>A temporary folder that is deleted on dispose.</summary>
    internal sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "drawplate-discovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
