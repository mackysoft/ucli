using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Applies shared bootstrap rules for writes under <c>.ucli/local</c>. </summary>
public static class UcliLocalStorageBootstrapper
{
    /// <summary> Gets the git-ignore entry used to exclude local runtime storage from version control. </summary>
    public const string LocalDirectoryIgnoreEntry = UcliStoragePathNames.LocalDirectoryName + "/";

    /// <summary> Ensures shared local-storage metadata exists when the target directory is under <c>.ucli/local</c>. </summary>
    /// <param name="directoryPath"> The target directory path. </param>
    public static void EnsureInitialized (AbsolutePath directoryPath)
    {
        if (!UcliStoragePathResolver.TryResolveLocalStorageRootDirectories(
                directoryPath,
                out var ucliDirectoryPath,
                out var localDirectoryPath))
        {
            return;
        }

        EnsureDirectoryIsNotReparsePointIfExists(ucliDirectoryPath!);
        DirectoryUtilities.Create(ucliDirectoryPath!);
        EnsureDirectoryIsNotReparsePointIfExists(ucliDirectoryPath!);

        EnsureLocalGitIgnoreExists(ucliDirectoryPath!);

        EnsureDirectoryIsNotReparsePointIfExists(localDirectoryPath!);
        DirectoryUtilities.Create(localDirectoryPath!);
        EnsureDirectoryIsNotReparsePointIfExists(localDirectoryPath!);
    }

    private static void EnsureDirectoryIsNotReparsePointIfExists (AbsolutePath directoryPath)
    {
        if (!DirectoryUtilities.Exists(directoryPath))
        {
            return;
        }

        var attributes = DirectoryUtilities.GetAttributes(directoryPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Local storage directory must not be a reparse point: {directoryPath}");
        }
    }

    private static void EnsureLocalGitIgnoreExists (AbsolutePath ucliDirectoryPath)
    {
        var gitIgnorePath = ContainedPath.Create(
            ucliDirectoryPath,
            RootRelativePath.Parse(UcliStoragePathNames.GitIgnoreFileName)).Target;
        if (FileUtilities.FileExists(gitIgnorePath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(
                FileSystemNativePathText.FromGuardedPath(gitIgnorePath),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(LocalDirectoryIgnoreEntry);
            writer.Write(Environment.NewLine);
        }
        catch (IOException) when (FileUtilities.FileExists(gitIgnorePath))
        {
        }
    }
}
