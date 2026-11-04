using System;

namespace Drawplate.Models.Planning;

public static class OutputNaming
{
    public static string TargetName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var dot = fileName.LastIndexOf('.');
        var stem = dot <= 0 ? fileName : fileName[..dot];
        return stem + ".mp4";
    }

    public static string PartialName(string fileName, int processId)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var dot = fileName.LastIndexOf('.');
        var stem = dot <= 0 ? fileName : fileName[..dot];
        return $".{stem}.{processId}.partial.mp4";
    }
}
