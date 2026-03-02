using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Resolves repository-root and shared <c>.ucli</c> storage paths. </summary>
public static class UcliStoragePathResolver
{
    /// <summary> Tries to resolve a repository root path by scanning parent directories for a <c>.git</c> marker. </summary>
    /// <param name="startPath"> The starting directory path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The repository root path when marker is found; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="startPath" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="startPath" /> exceeds platform path limits. </exception>
    public static string? TryResolveRepositoryRoot (string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            throw new ArgumentException("Start path must not be empty.", nameof(startPath));
        }

        var directoryPath = Path.GetFullPath(startPath);
        if (!Directory.Exists(directoryPath))
        {
            return null;
        }

        while (true)
        {
            var markerPath = Path.Combine(directoryPath, UcliStoragePathNames.GitMarkerName);
            if (Directory.Exists(markerPath) || File.Exists(markerPath))
            {
                return directoryPath;
            }

            var parentDirectory = Directory.GetParent(directoryPath);
            if (parentDirectory == null)
            {
                return null;
            }

            directoryPath = parentDirectory.FullName;
        }
    }

    /// <summary> Resolves storage root from a starting directory path. </summary>
    /// <param name="startPath"> The starting directory path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The repository root when found; otherwise the normalized absolute <paramref name="startPath" />. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="startPath" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="startPath" /> exceeds platform path limits. </exception>
    public static string ResolveStorageRoot (string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            throw new ArgumentException("Start path must not be empty.", nameof(startPath));
        }

        var normalizedStartPath = Path.GetFullPath(startPath);
        var repositoryRoot = TryResolveRepositoryRoot(normalizedStartPath);
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return repositoryRoot;
        }

        // NOTE:
        // Local and CI environments may not have a Git repository.
        // Use the starting path as a deterministic fallback storage root.
        return normalizedStartPath;
    }

    /// <summary> Resolves the absolute path to the <c>.ucli</c> directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute <c>.ucli</c> directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveUcliDirectoryPath (string storageRoot)
    {
        var normalizedStorageRoot = NormalizeStorageRoot(storageRoot);
        return Path.Combine(normalizedStorageRoot, UcliStoragePathNames.UcliDirectoryName);
    }

    /// <summary> Resolves the absolute path to shared <c>.ucli/config.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute config file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveConfigPath (string storageRoot)
    {
        return Path.Combine(ResolveUcliDirectoryPath(storageRoot), UcliStoragePathNames.ConfigFileName);
    }

    /// <summary> Resolves the absolute path to one fingerprint directory under <c>.ucli/local/fingerprints</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveFingerprintDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        var normalizedStorageRoot = NormalizeStorageRoot(storageRoot);
        var normalizedProjectFingerprint = NormalizeProjectFingerprint(projectFingerprint);

        return Path.Combine(
            normalizedStorageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.FingerprintsDirectoryName,
            normalizedProjectFingerprint);
    }

    /// <summary> Resolves the absolute path to daemon <c>session.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon session file path. </returns>
    public static string ResolveSessionPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SessionFileName);
    }

    /// <summary> Resolves the absolute path to daemon <c>daemon.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon log file path. </returns>
    public static string ResolveDaemonLogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonLogFileName);
    }

    /// <summary> Resolves the absolute path to daemon <c>lifecycle.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute lifecycle-lock file path. </returns>
    public static string ResolveLifecycleLockPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.LifecycleLockFileName);
    }

    /// <summary> Resolves the absolute path to plan-token key file under one fingerprint directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute plan-token key file path. </returns>
    public static string ResolvePlanTokenKeyPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.PlanTokenKeyFileName);
    }

    private static string NormalizeStorageRoot (string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        return Path.GetFullPath(storageRoot);
    }

    private static string NormalizeProjectFingerprint (string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        return StringValueNormalizer.TrimToNull(projectFingerprint)!;
    }
}