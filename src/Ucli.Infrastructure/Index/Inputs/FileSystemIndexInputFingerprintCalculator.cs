using System.Text;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes deterministic input fingerprints from filesystem sources. </summary>
internal sealed class FileSystemIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
{
    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        var normalizedProjectRoot = TryNormalizeProjectRoot(projectRootPath);
        if (normalizedProjectRoot is null)
        {
            return new ValueTask<IndexCoreInputHashSnapshot?>((IndexCoreInputHashSnapshot?)null);
        }

        return TryComputeCoreInternalAsync(normalizedProjectRoot, cancellationToken);
    }

    /// <summary> Tries to compute one input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public async ValueTask<IndexInputHashSnapshot?> TryComputeAsync (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        var normalizedProjectRoot = TryNormalizeProjectRoot(projectRootPath);
        if (normalizedProjectRoot is null)
        {
            return null;
        }

        var coreSnapshot = await TryComputeCoreInternalAsync(normalizedProjectRoot, cancellationToken).ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        var assetsPath = Path.Combine(normalizedProjectRoot, "Assets");

        var assetsContentHash = await TryHashDirectoryFilesAsync(assetsPath, "*", SearchOption.AllDirectories, cancellationToken).ConfigureAwait(false);
        if (assetsContentHash == null)
        {
            return null;
        }

        var assetSearchHash = ComputeUtf8Hash(string.Concat(
            coreSnapshot.CombinedHash,
            "\n",
            assetsContentHash));
        var guidPathHash = ComputeUtf8Hash(assetsContentHash);
        return new IndexInputHashSnapshot(
            ScriptAssembliesHash: coreSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: coreSnapshot.PackagesManifestHash,
            PackagesLockHash: coreSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: coreSnapshot.AssemblyDefinitionHash,
            AssetsContentHash: assetsContentHash,
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: coreSnapshot.CombinedHash);
    }

    private static async ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreInternalAsync (
        string normalizedProjectRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scriptAssembliesPath = Path.Combine(normalizedProjectRoot, "Library", "ScriptAssemblies");
        var packagesManifestPath = Path.Combine(normalizedProjectRoot, "Packages", "manifest.json");
        var packagesLockPath = Path.Combine(normalizedProjectRoot, "Packages", "packages-lock.json");
        var assetsPath = Path.Combine(normalizedProjectRoot, "Assets");
        var packagesPath = Path.Combine(normalizedProjectRoot, "Packages");

        var scriptAssembliesHash = await TryHashDirectoryFilesAsync(scriptAssembliesPath, "*", SearchOption.AllDirectories, cancellationToken).ConfigureAwait(false);
        if (scriptAssembliesHash == null)
        {
            return null;
        }

        var packagesManifestHash = await TryHashFileAsync(packagesManifestPath, cancellationToken).ConfigureAwait(false);
        if (packagesManifestHash == null)
        {
            return null;
        }

        var packagesLockHash = await TryHashFileAsync(packagesLockPath, cancellationToken).ConfigureAwait(false);
        if (packagesLockHash == null)
        {
            return null;
        }

        var assemblyDefinitionHash = await TryHashAssemblyDefinitionFilesAsync(assetsPath, packagesPath, cancellationToken).ConfigureAwait(false);
        if (assemblyDefinitionHash == null)
        {
            return null;
        }

        var combinedHash = ComputeUtf8Hash(string.Concat(
            scriptAssembliesHash,
            "\n",
            packagesManifestHash,
            "\n",
            packagesLockHash,
            "\n",
            assemblyDefinitionHash));
        return new IndexCoreInputHashSnapshot(
            ScriptAssembliesHash: scriptAssembliesHash,
            PackagesManifestHash: packagesManifestHash,
            PackagesLockHash: packagesLockHash,
            AssemblyDefinitionHash: assemblyDefinitionHash,
            CombinedHash: combinedHash);
    }

    private static string? TryNormalizeProjectRoot (string projectRootPath)
    {
        var pathResult = PathNormalizer.TryNormalizeFullPath(projectRootPath);
        return pathResult.IsSuccess ? pathResult.FullPath : null;
    }

    private static async ValueTask<string?> TryHashAssemblyDefinitionFilesAsync (
        string assetsPath,
        string packagesPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(assetsPath) || !Directory.Exists(packagesPath))
        {
            return null;
        }

        var files = new List<string>();
        if (!TryCollectFiles(assetsPath, "*.asmdef", files)
            || !TryCollectFiles(assetsPath, "*.asmref", files)
            || !TryCollectFiles(packagesPath, "*.asmdef", files)
            || !TryCollectFiles(packagesPath, "*.asmref", files))
        {
            return null;
        }

        files.Sort(StringComparer.Ordinal);
        return await TryHashFilesWithPathMetadataAsync(files, cancellationToken).ConfigureAwait(false);
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

        var buffer = new StringBuilder(files.Count * 80);
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = files[i];
            var fileHash = await TryHashFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (fileHash == null)
            {
                return null;
            }

            var normalizedPath = PathStringNormalizer.NormalizeAbsolutePathForHash(filePath);
            buffer.Append(normalizedPath);
            buffer.Append('\n');
            buffer.Append(fileHash);
            buffer.Append('\n');
        }

        return ComputeUtf8Hash(buffer.ToString());
    }

    private static async ValueTask<string?> TryHashFileAsync (
        string filePath,
        CancellationToken cancellationToken)
    {
        return await FileContentHash.TryComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeUtf8Hash (string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Sha256LowerHex.Compute(bytes);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
