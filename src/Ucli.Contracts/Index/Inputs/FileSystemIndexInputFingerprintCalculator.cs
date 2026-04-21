using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Paths;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Computes deterministic input fingerprints from filesystem sources. </summary>
internal sealed class FileSystemIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
{
    /// <summary> Tries to compute one core input fingerprint snapshot without asset lookup hashes. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCore (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        return TryComputeCoreInternal(Path.GetFullPath(projectRootPath), cancellationToken);
    }

    /// <summary> Tries to compute one input fingerprint snapshot from project files. </summary>
    /// <param name="projectRootPath"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public async ValueTask<IndexInputHashSnapshot?> TryCompute (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var coreSnapshot = await TryComputeCoreInternal(normalizedProjectRoot, cancellationToken).ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        var assetsPath = Path.Combine(normalizedProjectRoot, "Assets");

        var assetsContentHash = await TryHashDirectoryFiles(assetsPath, "*", SearchOption.AllDirectories, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreInternal (
        string normalizedProjectRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scriptAssembliesPath = Path.Combine(normalizedProjectRoot, "Library", "ScriptAssemblies");
        var packagesManifestPath = Path.Combine(normalizedProjectRoot, "Packages", "manifest.json");
        var packagesLockPath = Path.Combine(normalizedProjectRoot, "Packages", "packages-lock.json");
        var assetsPath = Path.Combine(normalizedProjectRoot, "Assets");
        var packagesPath = Path.Combine(normalizedProjectRoot, "Packages");

        var scriptAssembliesHash = await TryHashDirectoryFiles(scriptAssembliesPath, "*", SearchOption.AllDirectories, cancellationToken).ConfigureAwait(false);
        if (scriptAssembliesHash == null)
        {
            return null;
        }

        var packagesManifestHash = await TryHashFile(packagesManifestPath, cancellationToken).ConfigureAwait(false);
        if (packagesManifestHash == null)
        {
            return null;
        }

        var packagesLockHash = await TryHashFile(packagesLockPath, cancellationToken).ConfigureAwait(false);
        if (packagesLockHash == null)
        {
            return null;
        }

        var assemblyDefinitionHash = await TryHashAssemblyDefinitionFiles(assetsPath, packagesPath, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<string?> TryHashAssemblyDefinitionFiles (
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
        return await TryHashFilesWithPathMetadata(files, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<string?> TryHashDirectoryFiles (
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
        return await TryHashFilesWithPathMetadata(files, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<string?> TryHashFilesWithPathMetadata (
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
            var fileHash = await TryHashFile(filePath, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<string?> TryHashFile (
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return null;
        }

        return Sha256LowerHex.Compute(bytes);
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