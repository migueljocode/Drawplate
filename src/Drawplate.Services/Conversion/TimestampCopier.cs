using System;
using System.IO;

namespace Drawplate.Services.Conversion;

public static class TimestampCopier
{
    public static void Copy(string source, string destination, bool copyCreationTime = false)
    {
        var info = new FileInfo(source);
        var destInfo = new FileInfo(destination);

        destInfo.LastWriteTime = info.LastWriteTime;
        destInfo.LastAccessTime = info.LastAccessTime;

        if (copyCreationTime && OperatingSystem.IsWindows())
        {
            destInfo.CreationTime = info.CreationTime;
        }
    }
}
