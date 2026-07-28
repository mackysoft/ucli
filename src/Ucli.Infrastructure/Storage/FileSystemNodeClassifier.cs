using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Classifies filesystem node kinds that are not fully represented by <see cref="FileAttributes" />. </summary>
internal static class FileSystemNodeClassifier
{
    /// <summary> Returns whether the specified filesystem node is a regular file. </summary>
    /// <param name="filePath"> The path to inspect. </param>
    /// <param name="attributes"> The attributes already read for <paramref name="filePath" />. </param>
    /// <returns> <see langword="true" /> when the node is a regular file; otherwise <see langword="false" />. </returns>
    public static bool IsRegularFile (
        AbsolutePath filePath,
        FileAttributes attributes)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            return false;
        }

        return FileSystemNodeIdentityReader
            .ReadPath(filePath, "Filesystem node")
            .IsRegularFile;
    }
}
