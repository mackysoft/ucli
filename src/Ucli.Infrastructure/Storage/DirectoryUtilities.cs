using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Provides directory lifecycle operations over guarded absolute paths. </summary>
internal static class DirectoryUtilities
{
    /// <summary> Gets whether a directory currently exists at one guarded path. </summary>
    internal static bool Exists (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return Directory.Exists(FileSystemNativePathText.FromGuardedPath(path));
    }

    /// <summary> Ensures a directory exists at one guarded path. </summary>
    internal static void Create (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        Directory.CreateDirectory(FileSystemNativePathText.FromGuardedPath(path));
    }

    /// <summary> Gets the current filesystem attributes of one guarded directory path. </summary>
    internal static FileAttributes GetAttributes (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return File.GetAttributes(FileSystemNativePathText.FromGuardedPath(path));
    }

    /// <summary> Gets whether an existing guarded directory contains no filesystem entries. </summary>
    internal static bool IsEmpty (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return !Directory
            .EnumerateFileSystemEntries(FileSystemNativePathText.FromGuardedPath(path))
            .Any();
    }

    /// <summary> Enumerates the immediate child directory names of one guarded path. </summary>
    internal static IEnumerable<string> EnumerateDirectoryNames (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return new DirectoryInfo(FileSystemNativePathText.FromGuardedPath(path))
            .EnumerateDirectories()
            .Select(directory => directory.Name);
    }

    /// <summary> Deletes an existing directory at one guarded path and treats absence as a no-op. </summary>
    internal static void DeleteIfExists (
        AbsolutePath path,
        bool recursive = false)
    {
        EnsurePath(path, nameof(path));
        var nativePath = FileSystemNativePathText.FromGuardedPath(path);
        if (Directory.Exists(nativePath))
        {
            Directory.Delete(nativePath, recursive);
        }
    }

    /// <summary> Moves one guarded directory to another guarded path. </summary>
    internal static void Move (
        AbsolutePath source,
        AbsolutePath destination)
    {
        EnsurePath(source, nameof(source));
        EnsurePath(destination, nameof(destination));
        Directory.Move(
            FileSystemNativePathText.FromGuardedPath(source),
            FileSystemNativePathText.FromGuardedPath(destination));
    }

    private static void EnsurePath (
        AbsolutePath path,
        string parameterName)
    {
        if (path == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
}
