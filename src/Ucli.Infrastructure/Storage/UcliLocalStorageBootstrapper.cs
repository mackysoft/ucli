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
        Directory.CreateDirectory(ucliDirectoryPath!.Value);
        EnsureDirectoryIsNotReparsePointIfExists(ucliDirectoryPath!);

        EnsureLocalGitIgnoreExists(ucliDirectoryPath!);

        EnsureDirectoryIsNotReparsePointIfExists(localDirectoryPath!);
        Directory.CreateDirectory(localDirectoryPath!.Value);
        EnsureDirectoryIsNotReparsePointIfExists(localDirectoryPath!);
    }

    private static void EnsureDirectoryIsNotReparsePointIfExists (AbsolutePath directoryPath)
    {
        if (!Directory.Exists(directoryPath.Value))
        {
            return;
        }

        var attributes = File.GetAttributes(directoryPath.Value);
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
        if (File.Exists(gitIgnorePath.Value))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(
                gitIgnorePath.Value,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(LocalDirectoryIgnoreEntry);
            writer.Write(Environment.NewLine);
        }
        catch (IOException) when (File.Exists(gitIgnorePath.Value))
        {
        }
    }
}
