using System.Diagnostics.CodeAnalysis;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Resolves repository-root and shared <c>.ucli</c> storage paths. </summary>
public static class UcliStoragePathResolver
{
    private static readonly char[] PathSegmentInvalidPathChars =
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

        var directoryPath = NormalizePathArgument(startPath, nameof(startPath));
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

        var fullStartPath = NormalizePathArgument(startPath, nameof(startPath));
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

    /// <summary> Normalizes one storage-root path argument to an absolute path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The normalized absolute storage-root path. </returns>
    internal static string NormalizeStorageRootPath (string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        return NormalizePathArgument(storageRoot, nameof(storageRoot));
    }

    /// <summary> Resolves the absolute path to the <c>.ucli</c> directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute <c>.ucli</c> directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveUcliDirectoryPath (string storageRoot)
    {
        return ResolveUnderStorageRoot(storageRoot, UcliStoragePathNames.UcliDirectoryName);
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

        var currentDirectory = new DirectoryInfo(NormalizePathArgument(directoryPath, nameof(directoryPath)));
        while (currentDirectory != null)
        {
            var parentDirectory = currentDirectory.Parent;
            if (string.Equals(currentDirectory.Name, UcliStoragePathNames.LocalDirectoryName, PathStringNormalizer.CurrentPlatformPathComparison)
                && parentDirectory != null
                && string.Equals(parentDirectory.Name, UcliStoragePathNames.UcliDirectoryName, PathStringNormalizer.CurrentPlatformPathComparison))
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

    /// <summary> Resolves the absolute path to supervisor <c>manifest.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor manifest mutation lock file path. </returns>
    public static string ResolveSupervisorManifestLockPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorManifestLockFileName);
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
        var normalizedProjectFingerprint = NormalizeProjectFingerprint(projectFingerprint);

        return ResolveUnderStorageRoot(
            storageRoot,
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

    /// <summary> Resolves the absolute read-index writer lock path for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index writer lock path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveReadIndexWriteLockPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexWriteLockFileName);
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

    /// <summary> Resolves the absolute path to one read-index lookups directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/index/lookups</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index lookups directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexLookupsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.LookupsDirectoryName);
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

    /// <summary> Resolves the absolute path to one read-index ops describe artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index ops describe artifact directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveOpsDescribeDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.OpsDescribeDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index ops describe artifact file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="opKey"> The opaque operation describe key. Must be a SHA-256 lower-hex value. </param>
    /// <returns> The absolute read-index ops describe artifact file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is invalid. </exception>
    public static string ResolveOpsDescribePath (
        string storageRoot,
        string projectFingerprint,
        string opKey)
    {
        return Path.Combine(
            ResolveOpsDescribeDirectory(storageRoot, projectFingerprint),
            NormalizeOpsDescribeKey(opKey) + UcliStoragePathNames.OpsDescribeFileExtension);
    }

    /// <summary> Resolves the absolute path to one read-index asset-search lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index asset-search lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveAssetSearchLookupPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexLookupsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.AssetSearchLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index GUID-path lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index GUID-path lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveGuidPathLookupPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexLookupsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuidPathLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSceneTreeLiteLookupDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexLookupsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SceneTreeLiteLookupDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="scenePath"> The project-relative scene path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSceneTreeLiteLookupPath (
        string storageRoot,
        string projectFingerprint,
        string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            throw new ArgumentException("Scene path must not be empty.", nameof(scenePath));
        }

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        var sceneKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedScenePath));
        return Path.Combine(
            ResolveSceneTreeLiteLookupDirectory(storageRoot, projectFingerprint),
            sceneKey + UcliStoragePathNames.SceneTreeLiteLookupFileExtension);
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

    /// <summary> Resolves the absolute path to one fingerprint work directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/work</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint work directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveWorkDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.WorkDirectoryName);
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

    /// <summary> Resolves the absolute path to one fingerprint compile-artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/compile</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint compile-artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveCompileArtifactsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CompileArtifactsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one fingerprint build-artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/build</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint build-artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveBuildArtifactsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.BuildArtifactsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one fingerprint build-work directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/work/build</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint build-work directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveBuildWorkDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveWorkDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.BuildWorkDirectoryName);
    }

    /// <summary> Resolves the absolute path to one mutation read-postcondition file under one fingerprint directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute mutation read-postcondition file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveMutationReadPostconditionPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.MutationReadPostconditionFileName);
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
        var normalizedRunId = NormalizeRunId(runId);

        return Path.Combine(
            ResolveTestArtifactsDirectory(storageRoot, projectFingerprint),
            normalizedRunId);
    }

    /// <summary> Resolves the absolute path to one compile-run artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/compile/&lt;runId&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The run identifier value. Must not be <see langword="null" />, empty, whitespace, or contain path-segment/control tokens. </param>
    /// <returns> The absolute compile-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveCompileRunArtifactsDirectory (
        string storageRoot,
        string projectFingerprint,
        string runId)
    {
        var normalizedRunId = NormalizeRunId(runId);

        return Path.Combine(
            ResolveCompileArtifactsDirectory(storageRoot, projectFingerprint),
            normalizedRunId);
    }

    /// <summary> Resolves the absolute path to one build-run artifacts directory under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/artifacts/build/&lt;runId&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The run identifier value. Must not be <see langword="null" />, empty, whitespace, or contain path-segment/control tokens. </param>
    /// <returns> The absolute build-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveBuildRunArtifactsDirectory (
        string storageRoot,
        string projectFingerprint,
        string runId)
    {
        var normalizedRunId = NormalizeRunId(runId);

        return Path.Combine(
            ResolveBuildArtifactsDirectory(storageRoot, projectFingerprint),
            normalizedRunId);
    }

    /// <summary> Resolves the absolute runner output directory for one build run under <c>.ucli/local/fingerprints/&lt;projectFingerprint&gt;/work/build/&lt;runId&gt;/output</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The run identifier value. Must not be <see langword="null" />, empty, whitespace, or contain path-segment/control tokens. </param>
    /// <returns> The absolute runner output directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveBuildRunOutputDirectory (
        string storageRoot,
        string projectFingerprint,
        string runId)
    {
        var normalizedRunId = NormalizeRunId(runId);

        return Path.Combine(
            ResolveBuildWorkDirectory(storageRoot, projectFingerprint),
            normalizedRunId,
            UcliStoragePathNames.BuildOutputDirectoryName);
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

    /// <summary> Resolves the absolute daemon session-generation lock path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon session-generation lock path. </returns>
    public static string ResolveDaemonSessionLockPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonSessionLockFileName);
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

    /// <summary> Resolves the absolute path to daemon <c>daemon-lifecycle.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon lifecycle observation file path. </returns>
    public static string ResolveDaemonLifecyclePath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonLifecycleFileName);
    }

    /// <summary> Resolves the absolute path to GUI supervisor <c>gui-supervisor.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute GUI supervisor manifest path. </returns>
    public static string ResolveGuiSupervisorManifestPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestFileName);
    }

    /// <summary> Resolves the absolute GUI supervisor manifest lock path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute GUI supervisor manifest lock path. </returns>
    public static string ResolveGuiSupervisorManifestLockPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestLockFileName);
    }

    /// <summary> Resolves the absolute path to the daemon launch-attempts directory under one fingerprint directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon launch-attempts directory path. </returns>
    public static string ResolveLaunchAttemptsDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.LaunchAttemptsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one daemon launch-attempt directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="launchAttemptId"> The launch-attempt identifier. </param>
    /// <returns> The absolute daemon launch-attempt directory path. </returns>
    public static string ResolveLaunchAttemptDirectory (
        string storageRoot,
        string projectFingerprint,
        string launchAttemptId)
    {
        return Path.Combine(
            ResolveLaunchAttemptsDirectory(storageRoot, projectFingerprint),
            NormalizeLaunchAttemptId(launchAttemptId));
    }

    /// <summary> Resolves the absolute path to one daemon launch-attempt startup diagnosis file. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="launchAttemptId"> The launch-attempt identifier. </param>
    /// <returns> The absolute daemon launch-attempt startup diagnosis file path. </returns>
    public static string ResolveLaunchAttemptStartupDiagnosisPath (
        string storageRoot,
        string projectFingerprint,
        string launchAttemptId)
    {
        return Path.Combine(
            ResolveLaunchAttemptDirectory(storageRoot, projectFingerprint, launchAttemptId),
            UcliStoragePathNames.StartupDiagnosisFileName);
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

    private static string ResolveUnderStorageRoot (
        string storageRoot,
        params string[] relativeSegments)
    {
        var normalizedStorageRoot = NormalizeStorageRootPath(storageRoot);
        var pathSegments = new string[relativeSegments.Length + 1];
        pathSegments[0] = normalizedStorageRoot;
        Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);

        var candidatePath = Path.Combine(pathSegments);
        var repositoryPathResult = RepositoryPathNormalizer.TryNormalize(normalizedStorageRoot, candidatePath);
        if (!repositoryPathResult.IsSuccess)
        {
            throw new ArgumentException(repositoryPathResult.DiagnosticMessage, nameof(storageRoot));
        }

        return repositoryPathResult.FullPath!;
    }

    private static string NormalizePathArgument (
        string pathValue,
        string parameterName)
    {
        var pathResult = PathNormalizer.TryNormalizeFullPath(pathValue);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, parameterName);
        }

        return pathResult.FullPath!;
    }

    private static string NormalizeProjectFingerprint (string projectFingerprint)
    {
        if (!TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        if (normalizedProjectFingerprint.IndexOfAny(PathSegmentInvalidPathChars) >= 0
            || string.Equals(normalizedProjectFingerprint, ".", StringComparison.Ordinal)
            || string.Equals(normalizedProjectFingerprint, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Project fingerprint must be one path segment and must not contain path separator or traversal tokens.",
                nameof(projectFingerprint));
        }

        return normalizedProjectFingerprint;
    }

    private static string NormalizeRunId (string runId)
    {
        if (!TryTrimToNonEmpty(runId, out var normalizedRunId))
        {
            throw new ArgumentException("Run identifier must not be empty.", nameof(runId));
        }

        if (normalizedRunId.IndexOfAny(PathSegmentInvalidPathChars) >= 0
            || string.Equals(normalizedRunId, ".", StringComparison.Ordinal)
            || string.Equals(normalizedRunId, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Run identifier must be one path segment and must not contain path separator or traversal tokens.",
                nameof(runId));
        }

        return normalizedRunId;
    }

    private static string NormalizeLaunchAttemptId (string launchAttemptId)
    {
        if (!TryTrimToNonEmpty(launchAttemptId, out var normalizedLaunchAttemptId))
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(launchAttemptId));
        }

        if (normalizedLaunchAttemptId.IndexOfAny(PathSegmentInvalidPathChars) >= 0
            || string.Equals(normalizedLaunchAttemptId, ".", StringComparison.Ordinal)
            || string.Equals(normalizedLaunchAttemptId, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Launch attempt identifier must be one path segment and must not contain path separator or traversal tokens.",
                nameof(launchAttemptId));
        }

        return normalizedLaunchAttemptId;
    }

    private static string NormalizeOpsDescribeKey (string opKey)
    {
        if (!TryTrimToNonEmpty(opKey, out var normalizedOpKey))
        {
            throw new ArgumentException("Ops describe key must not be empty.", nameof(opKey));
        }

        if (normalizedOpKey.Length != 64)
        {
            throw new ArgumentException("Ops describe key must be a SHA-256 lower-hex value.", nameof(opKey));
        }

        for (var i = 0; i < normalizedOpKey.Length; i++)
        {
            var c = normalizedOpKey[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                throw new ArgumentException("Ops describe key must be a SHA-256 lower-hex value.", nameof(opKey));
            }
        }

        return normalizedOpKey;
    }

    private static bool TryTrimToNonEmpty (
        string? value,
        [NotNullWhen(true)] out string? normalizedValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalizedValue = null;
            return false;
        }

        normalizedValue = value.Trim();
        return true;
    }
}
