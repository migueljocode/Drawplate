using System;
using System.IO;

using Drawplate.Models.Planning;

namespace Drawplate.Services.Conversion;

public sealed class RealPlanFileSystem : IPlanFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string FullPath(string path) => Path.GetFullPath(path);

    public string ResolveLinks(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return path;
    }
}
