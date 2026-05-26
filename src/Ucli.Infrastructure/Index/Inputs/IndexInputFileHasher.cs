using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Hashes read-index input files using deterministic path metadata ordering. </summary>
internal static class IndexInputFileHasher
{
    /// <summary> Computes the content hash for all asset files. </summary>
    public static ValueTask<string?> TryHashAssetsContentAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        return TryHashDirectoryContentAsync(sourcePaths.AssetsPath, cancellationToken);
    }

    /// <summary> Computes the content hash for all files under one directory. </summary>
    public static ValueTask<string?> TryHashDirectoryContentAsync (
        string directoryPath,
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
        var packagesManifestHash = await TryHashFileAsync(sourcePaths.PackagesManifestPath, cancellationToken).ConfigureAwait(false);
        var packagesLockHash = await TryHashFileAsync(sourcePaths.PackagesLockPath, cancellationToken).ConfigureAwait(false);
        var assemblyDefinitionHash = await TryHashAssemblyDefinitionFilesAsync(sourcePaths, cancellationToken).ConfigureAwait(false);
        return TryCreateCoreInputFileHashes(
            scriptAssembliesHash,
            packagesManifestHash,
            packagesLockHash,
            assemblyDefinitionHash);
    }

    /// <summary> Computes the combined hash for all assembly definition and assembly reference files. </summary>
    public static async ValueTask<string?> TryHashAssemblyDefinitionFilesAsync (
        IndexInputSourcePaths sourcePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(sourcePaths.AssetsPath) || !Directory.Exists(sourcePaths.PackagesPath))
        {
            return null;
        }

        var files = new List<string>();
        if (!TryCollectFiles(sourcePaths.AssetsPath, "*.asmdef", files)
            || !TryCollectFiles(sourcePaths.AssetsPath, "*.asmref", files)
            || !TryCollectFiles(sourcePaths.PackagesPath, "*.asmdef", files)
            || !TryCollectFiles(sourcePaths.PackagesPath, "*.asmref", files))
        {
            return null;
        }

        files.Sort(StringComparer.Ordinal);
        return await TryHashFilesWithPathMetadataAsync(files, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Computes one file content hash. </summary>
    public static async ValueTask<string?> TryHashFileAsync (
        string filePath,
        CancellationToken cancellationToken)
    {
        return await FileContentHash.TryComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Computes a SHA-256 lower-hex hash for one UTF-8 text value. </summary>
    public static string ComputeUtf8Hash (string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using var hashWriter = new Utf8Sha256HashWriter();
        hashWriter.Append(text);
        return hashWriter.GetHashAndReset();
    }

    private static async ValueTask<string?> TryHashDirectoryFilesAsync (
        string directoryPath,
        string searchPattern,
        SearchOption searchOption,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(directoryPath))
        {
            return null;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath, searchPattern, searchOption);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return null;
        }

        Array.Sort(files, StringComparer.Ordinal);
        return await TryHashFilesWithPathMetadataAsync(files, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryCollectFiles (
        string directoryPath,
        string searchPattern,
        List<string> destination)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return false;
        }

        if (files.Length > 0)
        {
            destination.AddRange(files);
        }

        return true;
    }

    private static async ValueTask<string?> TryHashFilesWithPathMetadataAsync (
        IReadOnlyList<string> files,
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
        string? scriptAssembliesHash,
        string? packagesManifestHash,
        string? packagesLockHash,
        string? assemblyDefinitionHash)
    {
        return scriptAssembliesHash == null
            || packagesManifestHash == null
            || packagesLockHash == null
            || assemblyDefinitionHash == null
                ? null
                : new IndexCoreInputFileHashes(
                    ScriptAssembliesHash: scriptAssembliesHash,
                    PackagesManifestHash: packagesManifestHash,
                    PackagesLockHash: packagesLockHash,
                    AssemblyDefinitionHash: assemblyDefinitionHash);
    }

    private static async ValueTask<bool> TryAppendFileHashMetadataAsync (
        Utf8Sha256HashWriter hashWriter,
        string filePath,
        CancellationToken cancellationToken)
    {
        var fileHash = await TryHashFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (fileHash == null)
        {
            return false;
        }

        var normalizedPath = PathStringNormalizer.NormalizeAbsolutePathForHash(filePath);
        hashWriter.Append(normalizedPath);
        hashWriter.Append('\n');
        hashWriter.Append(fileHash);
        hashWriter.Append('\n');
        return true;
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
