using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Drawplate.Models.Encoding;
using Drawplate.Models.Planning;

using Xunit;

namespace Drawplate.Tests.Unit;

public class FakePlanFileSystem : IPlanFileSystem
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    // Keys are full paths so that "/out/clip.mp4" and the planner's Path.Combine result compare equal on every OS.
    public void AddFile(string path) => _files.Add(Path.GetFullPath(path));
    public void AddDirectory(string path) => _directories.Add(Path.GetFullPath(path));

    public bool FileExists(string path) => _files.Contains(Path.GetFullPath(path));
    public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path));
    public string FullPath(string path) => Path.GetFullPath(path);
    public string ResolveLinks(string path) => path;
}

public class PlannerTests
{
    [Fact]
    public void Missing_File()
    {
        var fs = new FakePlanFileSystem();
        var plan = ConversionPlanner.Build(["missing.mp4"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Single(plan.Files);
        Assert.Equal(PlanStatus.Missing, plan.Files[0].Status);
    }

    [Fact]
    public void Directory_Argument()
    {
        var fs = new FakePlanFileSystem();
        fs.AddDirectory("/somedir");
        var plan = ConversionPlanner.Build(["/somedir"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Equal(PlanStatus.NotAFile, plan.Files[0].Status);
    }

    [Fact]
    public void Unsupported_Extension()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/file.txt");
        var plan = ConversionPlanner.Build(["/file.txt"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Equal(PlanStatus.SkipUnsupported, plan.Files[0].Status);
    }

    [Fact]
    public void InsideOutput_Skipped()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/out/clip.mp4");
        var plan = ConversionPlanner.Build(["/out/clip.mp4"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Equal(PlanStatus.SkipInsideOutput, plan.Files[0].Status);
    }

    [Fact]
    public void ExistingTarget_Skipped()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/in/clip.mp4");
        fs.AddFile("/out/clip.mp4");
        var plan = ConversionPlanner.Build(["/in/clip.mp4"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Equal(PlanStatus.SkipExists, plan.Files[0].Status);
    }

    [Fact]
    public void Collision_Detected_ClipMp4_ClipMov()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/x/clip.mp4");
        fs.AddFile("/y/clip.mov");
        var plan = ConversionPlanner.Build(["/x/clip.mp4", "/y/clip.mov"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Single(plan.Collisions);
        Assert.Equal("clip.mp4", plan.Collisions[0].OutputName);
        Assert.Equal(2, plan.Collisions[0].Sources.Length);
    }

    [Fact]
    public void Collision_CaseInsensitive()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/x/clip.mp4");
        fs.AddFile("/y/CLIP.MOV");
        var plan = ConversionPlanner.Build(["/x/clip.mp4", "/y/CLIP.MOV"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Single(plan.Collisions);
    }

    [Fact]
    public void NoCollision_DifferentStems()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/x/a.mp4");
        fs.AddFile("/y/b.mov");
        var plan = ConversionPlanner.Build(["/x/a.mp4", "/y/b.mov"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Empty(plan.Collisions);
    }

    [Fact]
    public void Order_Preserved()
    {
        var fs = new FakePlanFileSystem();
        fs.AddFile("/b.mp4");
        fs.AddFile("/a.mp4");
        fs.AddFile("/c.mp4");
        var plan = ConversionPlanner.Build(["/b.mp4", "/a.mp4", "/c.mp4"], "/out", fs, OperatingSystemKind.Linux);
        Assert.Equal(["/b.mp4", "/a.mp4", "/c.mp4"], plan.Files.Select(f => f.Source).ToArray());
    }
}
