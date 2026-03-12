using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Resolves repository-root and shared <c>.ucli</c> storage paths. </summary>
public static class UcliStoragePathResolver
{
    private static readonly char[] RunIdInvalidPathChars =
    {
        '/',
        '\\',
        ':',
    };

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

        var fullStartPath = Path.GetFullPath(startPath);
        var repositoryRoot = TryResolveRepositoryRoot(fullStartPath);
        if (repositoryRoot is not null)
        {
            return repositoryRoot;
        }

        // NOTE:
        // Local and CI environments may not have a Git repository.
        // Use the starting path as a deterministic fallback storage root.
        return fullStartPath;
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

    /// <summary> Resolves the absolute path to the <c>.ucli/local</c> directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute <c>.ucli/local</c> directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveLocalDirectoryPath (string storageRoot)
    {
        return Path.Combine(ResolveUcliDirectoryPath(storageRoot), UcliStoragePathNames.LocalDirectoryName);
    }

    /// <summary> Tries to resolve the shared <c>.ucli</c> and <c>.ucli/local</c> roots that own a directory path. </summary>
    /// <param name="directoryPath"> The directory path that may be under <c>.ucli/local</c>. </param>
    /// <param name="ucliDirectoryPath"> The resolved shared <c>.ucli</c> directory path when matched. </param>
    /// <param name="localDirectoryPath"> The resolved shared <c>.ucli/local</c> directory path when matched. </param>
    /// <returns> <see langword="true" /> when <paramref name="directoryPath" /> is under <c>.ucli/local</c>; otherwise <see langword="false" />. </returns>
    internal static bool TryResolveLocalStorageRootDirectories (
        string directoryPath,
        out string? ucliDirectoryPath,
        out string? localDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("directoryPath must not be empty.", nameof(directoryPath));
        }

        var comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var currentDirectory = new DirectoryInfo(Path.GetFullPath(directoryPath));
        while (currentDirectory != null)
        {
            var parentDirectory = currentDirectory.Parent;
            if (string.Equals(currentDirectory.Name, UcliStoragePathNames.LocalDirectoryName, comparison)
                && parentDirectory != null
                && string.Equals(parentDirectory.Name, UcliStoragePathNames.UcliDirectoryName, comparison))
            {
                ucliDirectoryPath = parentDirectory.FullName;
                localDirectoryPath = currentDirectory.FullName;
                return true;
            }

            currentDirectory = parentDirectory;
        }

        ucliDirectoryPath = null;
        localDirectoryPath = null;
        return false;
    }

    /// <summary> Resolves the absolute path to the <c>.ucli/local/supervisor</c> directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute supervisor runtime directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSupervisorDirectoryPath (string storageRoot)
    {
        return Path.Combine(
            ResolveLocalDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorDirectoryName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>manifest.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor manifest file path. </returns>
    public static string ResolveSupervisorManifestPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorManifestFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>bootstrap.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor bootstrap lock file path. </returns>
    public static string ResolveSupervisorBootstrapLockPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorBootstrapLockFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>supervisor.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor log file path. </returns>
    public static string ResolveSupervisorLogPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorLogFileName);
    }

    /// <summary> Resolves the absolute path to the launch-agent plist used for supervisor bootstrap. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute launch-agent plist path. </returns>
    public static string ResolveSupervisorLaunchAgentPlistPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorLaunchAgentPlistFileName);
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

    /// <summary> Resolves the absolute path to one read-index directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/index</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.IndexDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index catalogs directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/index/catalogs</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index catalogs directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexCatalogsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CatalogsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index types catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index types catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveTypesCatalogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TypesCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index schemas catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index schemas catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSchemasCatalogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SchemasCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index ops catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index ops catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveOpsCatalogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.OpsCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index inputs manifest file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index inputs manifest file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexInputsManifestPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.IndexInputsDirectoryName,
            UcliStoragePathNames.IndexInputsManifestFileName);
    }

    /// <summary> Resolves the absolute path to one fingerprint artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveArtifactsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one fingerprint test-artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/test</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint test-artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveTestArtifactsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TestArtifactsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one test-run artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/test/&lt;runId&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The run identifier value. Must not be <see langword="null" />, empty, whitespace, or contain path-segment/control tokens. </param>
    /// <returns> The absolute test-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveTestRunArtifactsDirectory (
        string storageRoot,
        string projectFingerprint,
        string runId)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(runId, out var normalizedRunId))
        {
            throw new ArgumentException("Run identifier must not be empty.", nameof(runId));
        }

        if (normalizedRunId.IndexOfAny(RunIdInvalidPathChars) >= 0
            || string.Equals(normalizedRunId, ".", StringComparison.Ordinal)
            || string.Equals(normalizedRunId, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Run identifier must be one path segment and must not contain path separator or traversal tokens.",
                nameof(runId));
        }

        return Path.Combine(
            ResolveTestArtifactsDirectory(storageRoot, projectFingerprint),
            normalizedRunId);
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

    /// <summary> Resolves the absolute path to daemon <c>daemon-diagnosis.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon diagnosis file path. </returns>
    public static string ResolveDaemonDiagnosisPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonDiagnosisFileName);
    }

    /// <summary> Resolves the absolute path to the uCLI Unity plugin marker cache file under one fingerprint directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute plugin marker cache file path. </returns>
    public static string ResolveUnityUcliPluginMarkerCachePath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName);
    }

    /// <summary> Resolves the absolute path to Unity batchmode <c>unity.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute Unity log file path. </returns>
    public static string ResolveUnityLogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityLogFileName);
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
        if (!StringValueNormalizer.TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        return normalizedProjectFingerprint;
    }
}