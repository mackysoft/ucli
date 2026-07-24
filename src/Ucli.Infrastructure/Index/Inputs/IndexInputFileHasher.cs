using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Hashes read-index input files using deterministic path metadata ordering. </summary>
internal static class IndexInputFileHasher
{
    /// <summary> Computes the content hash for all files under one directory. </summary>
    public static ValueTask<Sha256Digest?> TryHashDirectoryContentAsync (
        AbsolutePath directoryPath,
        CancellationToken cancellationToken)
    {
        return TryHashDirectoryFilesAsync(
            directoryPath,
            "*",
            SearchOption.AllDirectories,
            cancellationToken);
    }

    /// <summary> Computes all core input file hashes required to build a core input snapshot. </summary>
    public static async ValueTask<IndexCoreInputFileHashes?> TryHashCoreInputsAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scriptAssembliesHash = await TryHashDirectoryContentAsync(sourcePaths.ScriptAssembliesPath, cancellationToken).ConfigureAwait(false);
        var packagesManifestHash = await FileContentHash.TryComputeFileHashAsync(sourcePaths.PackagesManifestPath, cancellationToken).ConfigureAwait(false);
        var packagesLockHash = await FileContentHash.TryComputeFileHashAsync(sourcePaths.PackagesLockPath, cancellationToken).ConfigureAwait(false);
        var assemblyDefinitionHash = await TryHashAssemblyDefinitionFilesAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
        return TryCreateCoreInputFileHashes(
            scriptAssembliesHash,
            packagesManifestHash,
            packagesLockHash,
            assemblyDefinitionHash);
    }

    /// <summary> Computes the combined hash for all assembly definition and assembly reference files. </summary>
    public static async ValueTask<Sha256Digest?> TryHashAssemblyDefinitionFilesAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(sourcePaths.AssetsPath.Value) || !Directory.Exists(sourcePaths.PackagesPath.Value))
        {
            return null;
        }

        var files = new List<FileHashInput>();
        if (!TryCollectFiles(sourcePaths.AssetsPath, "*.asmdef", files)
            || !TryCollectFiles(sourcePaths.AssetsPath, "*.asmref", files)
            || !TryCollectFiles(sourcePaths.PackagesPath, "*.asmdef", files)
            || !TryCollectFiles(sourcePaths.PackagesPath, "*.asmref", files))
        {
            return null;
        }

        files.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.IdentityText, right.IdentityText));
        return await TryHashFilesWithPathMetadataAsync(files, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Computes a SHA-256 lower-hex hash for one UTF-8 text value. </summary>
    public static Sha256Digest ComputeUtf8Hash (string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using var hashWriter = new Utf8Sha256HashWriter();
        hashWriter.Append(text);
        return hashWriter.GetHashAndReset();
    }

    private static async ValueTask<Sha256Digest?> TryHashDirectoryFilesAsync (
        AbsolutePath directoryPath,
        string searchPattern,
        SearchOption searchOption,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(directoryPath.Value))
        {
            return null;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath.Value, searchPattern, searchOption);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return null;
        }

        var guardedFiles = new FileHashInput[files.Length];
        for (var index = 0; index < files.Length; index++)
        {
            guardedFiles[index] = FileHashInput.Create(AbsolutePath.Parse(files[index]));
        }

        Array.Sort(guardedFiles, static (left, right) => StringComparer.Ordinal.Compare(left.IdentityText, right.IdentityText));
        return await TryHashFilesWithPathMetadataAsync(guardedFiles, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryCollectFiles (
        AbsolutePath directoryPath,
        string searchPattern,
        List<FileHashInput> destination)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath.Value, searchPattern, SearchOption.AllDirectories);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return false;
        }

        if (files.Length > 0)
        {
            for (var index = 0; index < files.Length; index++)
            {
                destination.Add(FileHashInput.Create(AbsolutePath.Parse(files[index])));
            }
        }

        return true;
    }

    private static async ValueTask<Sha256Digest?> TryHashFilesWithPathMetadataAsync (
        IReadOnlyList<FileHashInput> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (files.Count == 0)
        {
            return ComputeUtf8Hash(string.Empty);
        }

        using var hashWriter = new Utf8Sha256HashWriter();
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await TryAppendFileHashMetadataAsync(hashWriter, files[i], cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        return hashWriter.GetHashAndReset();
    }

    private static IndexCoreInputFileHashes? TryCreateCoreInputFileHashes (
        Sha256Digest? scriptAssembliesHash,
        Sha256Digest? packagesManifestHash,
        Sha256Digest? packagesLockHash,
        Sha256Digest? assemblyDefinitionHash)
    {
        return scriptAssembliesHash == null
            || packagesManifestHash == null
            || packagesLockHash == null
            || assemblyDefinitionHash == null
                ? null
                : new IndexCoreInputFileHashes(
                    scriptAssembliesHash,
                    packagesManifestHash,
                    packagesLockHash,
                    assemblyDefinitionHash);
    }

    private static async ValueTask<bool> TryAppendFileHashMetadataAsync (
        Utf8Sha256HashWriter hashWriter,
        FileHashInput file,
        CancellationToken cancellationToken)
    {
        var fileHash = await FileContentHash.TryComputeFileHashAsync(file.Path, cancellationToken).ConfigureAwait(false);
        if (fileHash == null)
        {
            return false;
        }

        hashWriter.Append(file.IdentityText);
        hashWriter.Append('\n');
        hashWriter.Append(fileHash.ToString());
        hashWriter.Append('\n');
        return true;
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }

    private readonly record struct FileHashInput (
        AbsolutePath Path,
        string IdentityText)
    {
        public static FileHashInput Create (AbsolutePath path)
        {
            return new FileHashInput(
                path,
                DeterministicPathText.ForIdentity(path));
        }
    }
}
