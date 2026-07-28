using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Provides directory lifecycle operations over guarded absolute paths. </summary>
internal static class DirectoryUtilities
{
    /// <summary> Gets whether a directory currently exists at one guarded path. </summary>
    internal static bool Exists (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return Directory.Exists(path.Value);
    }

    /// <summary> Ensures a directory exists at one guarded path. </summary>
    internal static void Create (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        Directory.CreateDirectory(path.Value);
    }

    /// <summary> Gets the current filesystem attributes of one guarded directory path. </summary>
    internal static FileAttributes GetAttributes (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return File.GetAttributes(path.Value);
    }

    /// <summary> Gets whether an existing guarded directory contains no filesystem entries. </summary>
    internal static bool IsEmpty (AbsolutePath path)
    {
        EnsurePath(path, nameof(path));
        return !Directory.EnumerateFileSystemEntries(path.Value).Any();
    }

    /// <summary> Deletes an existing directory at one guarded path and treats absence as a no-op. </summary>
    internal static void DeleteIfExists (
        AbsolutePath path,
        bool recursive = false)
    {
        EnsurePath(path, nameof(path));
        if (Directory.Exists(path.Value))
        {
            Directory.Delete(path.Value, recursive);
        }
    }

    /// <summary> Moves one guarded directory to another guarded path. </summary>
    internal static void Move (
        AbsolutePath source,
        AbsolutePath destination)
    {
        EnsurePath(source, nameof(source));
        EnsurePath(destination, nameof(destination));
        Directory.Move(source.Value, destination.Value);
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
