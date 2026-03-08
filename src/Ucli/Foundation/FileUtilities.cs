namespace MackySoft.Ucli.Foundation;

/// <summary> Provides shared utility operations for filesystem files. </summary>
internal static class FileUtilities
{
    /// <summary> Deletes one file and treats a missing file as a valid no-op state. </summary>
    /// <param name="path"> The target file path. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is invalid. </exception>
    public static void DeleteIfExists (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}