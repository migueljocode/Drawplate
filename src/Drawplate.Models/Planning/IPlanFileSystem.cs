namespace Drawplate.Models.Planning;

public interface IPlanFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string FullPath(string path);
    string ResolveLinks(string path);
}
