#if !NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using System.Text;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Resolves repository-root and shared <c>.ucli</c> storage paths. </summary>
public static class UcliStoragePathResolver
{
    /// <summary> Gets the longest Windows storage-root path supported by all fixed uCLI-managed paths. </summary>
    internal const int MaximumSupportedWindowsStorageRootLength = 112;

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
            EnsureSupportedStorageRootLength(repositoryRoot);
            return repositoryRoot;
        }

        // NOTE:
        // Local and CI environments may not have a Git repository.
        // Use the starting path as a deterministic fallback storage root.
        EnsureSupportedStorageRootLength(fullStartPath);
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

        var normalizedStorageRoot = NormalizePathArgument(storageRoot, nameof(storageRoot));
        EnsureSupportedStorageRootLength(normalizedStorageRoot);
        return normalizedStorageRoot;
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

    /// <summary> Resolves the absolute path to supervisor <c>runtime-ownership.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor runtime ownership lock file path. </returns>
    public static string ResolveSupervisorRuntimeOwnershipLockPath (string storageRoot)
    {
        return Path.Combine(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorRuntimeOwnershipLockFileName);
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

    /// <summary> Resolves one project-scoped directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped storage directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveProjectDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderStorageRoot(
            storageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.ProjectsDirectoryName,
            StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint));
    }

    /// <summary> Resolves one read-index directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/index</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.IndexDirectoryName);
    }

    /// <summary> Resolves the absolute read-index writer lock path for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index writer lock path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveReadIndexWriteLockPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexWriteLockFileName);
    }

    /// <summary> Resolves the atomic pointer to the current immutable read-index generation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute current-generation pointer path. </returns>
    public static string ResolveReadIndexCurrentGenerationPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexCurrentGenerationFileName);
    }

    /// <summary> Resolves the directory containing immutable read-index generations. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute generation-root directory path. </returns>
    public static string ResolveReadIndexGenerationsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName);
    }

    /// <summary> Resolves one immutable read-index generation directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute immutable generation directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    public static string ResolveReadIndexGenerationDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexGenerationsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves the directory containing unpublished read-index generations. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute staging-root directory path. </returns>
    internal static string ResolveReadIndexStagingDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexStagingDirectoryName);
    }

    /// <summary> Resolves one unpublished read-index generation directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute staging generation directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    internal static string ResolveReadIndexStagingGenerationDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexStagingDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves the directory containing generation-retention markers. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute retention-marker directory path. </returns>
    internal static string ResolveReadIndexRetentionDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexRetentionDirectoryName);
    }

    /// <summary> Resolves the deletion-eligibility marker for one immutable generation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute retention-marker path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    internal static string ResolveReadIndexRetentionMarkerPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexRetentionDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves one read-index catalogs directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/index/catalogs</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index catalogs directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexCatalogsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CatalogsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index types catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index types catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveTypesCatalogPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TypesCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index schemas catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index schemas catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSchemasCatalogPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SchemasCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index ops catalog file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index ops catalog file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveOpsCatalogPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.OpsCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index ops describe artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index ops describe artifact directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveOpsDescribeDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexOpsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index ops describe artifact file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="opKey"> The operation describe content digest. </param>
    /// <returns> The absolute read-index ops describe artifact file path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> or <paramref name="opKey" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is invalid. </exception>
    public static string ResolveOpsDescribePath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Sha256Digest opKey)
    {
        if (opKey == null)
        {
            throw new ArgumentNullException(nameof(opKey));
        }

        return Path.Combine(
            ResolveOpsDescribeDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeSha256Digest(opKey)
                + UcliStoragePathNames.OpsDescribeFileExtension);
    }

    /// <summary> Resolves the absolute path to one read-index asset-search lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index asset-search lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveAssetSearchLookupPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.AssetSearchLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index GUID-path lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index GUID-path lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveGuidPathLookupPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.GuidPathLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSceneTreeLiteLookupDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexScenesDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="scenePath"> The project-relative scene path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveSceneTreeLiteLookupPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            throw new ArgumentException("Scene path must not be empty.", nameof(scenePath));
        }

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        var sceneKey = Sha256Digest.Compute(Encoding.UTF8.GetBytes(normalizedScenePath));
        return Path.Combine(
            ResolveSceneTreeLiteLookupDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeSha256Digest(sceneKey)
                + UcliStoragePathNames.SceneTreeLiteLookupFileExtension);
    }

    /// <summary> Resolves the absolute path to one read-index inputs manifest file. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index inputs manifest file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveIndexInputsManifestPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return Path.Combine(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.IndexInputsManifestFileName);
    }

    /// <summary> Resolves one artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    /// <summary> Resolves one work directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/work</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped work directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveWorkDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.WorkDirectoryName);
    }

    /// <summary> Resolves one test-artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/test</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped test-artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveTestArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TestArtifactsDirectoryName);
    }

    /// <summary> Resolves one compile-artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/compile</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped compile-artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveCompileArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CompileArtifactsDirectoryName);
    }

    /// <summary> Resolves the project-scoped screenshot artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute screenshot artifact directory path. </returns>
    public static string ResolveScreenshotArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ScreenshotDirectoryName);
    }

    /// <summary> Resolves one capture-scoped screenshot artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute capture artifact directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static string ResolveScreenshotCaptureArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return Path.Combine(
            ResolveScreenshotArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(captureId, nameof(captureId)));
    }

    /// <summary> Resolves one final screenshot PNG artifact path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute screenshot PNG artifact path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static string ResolveScreenshotCaptureArtifactPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return Path.Combine(
            ResolveScreenshotCaptureArtifactsDirectory(storageRoot, projectFingerprint, captureId),
            UcliStoragePathNames.ScreenshotPngFileName);
    }

    /// <summary> Resolves the project-scoped screenshot work directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute screenshot work directory path. </returns>
    public static string ResolveScreenshotWorkDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveWorkDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ScreenshotDirectoryName);
    }

    /// <summary> Resolves one capture-scoped screenshot staging directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute capture staging directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static string ResolveScreenshotCaptureStagingDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return Path.Combine(
            ResolveScreenshotWorkDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(captureId, nameof(captureId)));
    }

    /// <summary> Resolves one normalized raw screenshot staging file path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute raw screenshot staging file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static string ResolveScreenshotCaptureRawStagingPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return Path.Combine(
            ResolveScreenshotCaptureStagingDirectory(storageRoot, projectFingerprint, captureId),
            UcliStoragePathNames.ScreenshotRawStagingFileName);
    }

    /// <summary> Resolves the mutation read-postcondition file under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute mutation read-postcondition file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when any argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveMutationReadPostconditionPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.MutationReadPostconditionFileName);
    }

    /// <summary> Resolves one test-run directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/test/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute test-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace, or when <paramref name="runId" /> is empty. </exception>
    public static string ResolveTestRunArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid runId)
    {
        return Path.Combine(
            ResolveTestArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId)));
    }

    /// <summary> Resolves one compile-run directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/compile/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute compile-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace, or when <paramref name="runId" /> is empty. </exception>
    public static string ResolveCompileRunArtifactsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid runId)
    {
        return Path.Combine(
            ResolveCompileArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId)));
    }

    /// <summary> Resolves one build-run directory under <c>.ucli/local/build-runs/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute build-run storage directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace, or when <paramref name="runId" /> is empty. </exception>
    public static string ResolveBuildRunDirectory (
        string storageRoot,
        Guid runId)
    {
        return ResolveUnderStorageRoot(
            storageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.BuildRunsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId)));
    }

    /// <summary> Resolves one build-run artifacts directory under <c>.ucli/local/build-runs/&lt;runStorageKey&gt;/artifacts</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute build-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace, or when <paramref name="runId" /> is empty. </exception>
    public static string ResolveBuildRunArtifactsDirectory (
        string storageRoot,
        Guid runId)
    {
        return Path.Combine(
            ResolveBuildRunDirectory(storageRoot, runId),
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    /// <summary> Resolves one runner output directory under <c>.ucli/local/build-runs/&lt;runStorageKey&gt;/work/output</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute runner output directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />, empty, or whitespace, or when <paramref name="runId" /> is empty. </exception>
    public static string ResolveBuildRunOutputDirectory (
        string storageRoot,
        Guid runId)
    {
        return Path.Combine(
            ResolveBuildRunDirectory(storageRoot, runId),
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    /// <summary> Resolves the absolute path to daemon <c>session.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute daemon session file path. </returns>
    public static string ResolveSessionPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SessionFileName);
    }

    /// <summary> Resolves the absolute oneshot bootstrap-envelope directory for one project fingerprint. </summary>
    public static string ResolveOneshotBootstrapDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.OneshotBootstrapDirectoryName);
    }

    /// <summary> Resolves the absolute path for one non-empty oneshot bootstrap identifier. </summary>
    public static string ResolveOneshotBootstrapPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid bootstrapId)
    {
        return Path.Combine(
            ResolveOneshotBootstrapDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(bootstrapId, nameof(bootstrapId))
                + UcliStoragePathNames.OneshotBootstrapFileExtension);
    }

    /// <summary> Resolves the absolute daemon session-generation lock path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute daemon session-generation lock path. </returns>
    public static string ResolveDaemonSessionLockPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonSessionLockFileName);
    }

    /// <summary> Resolves the absolute path to daemon <c>daemon-diagnosis.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute daemon diagnosis file path. </returns>
    public static string ResolveDaemonDiagnosisPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonDiagnosisFileName);
    }

    /// <summary> Resolves the absolute path to daemon <c>daemon-lifecycle.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute daemon lifecycle observation file path. </returns>
    public static string ResolveDaemonLifecyclePath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.DaemonLifecycleFileName);
    }

    /// <summary> Resolves the absolute path to GUI supervisor <c>gui-supervisor.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute GUI supervisor manifest path. </returns>
    public static string ResolveGuiSupervisorManifestPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestFileName);
    }

    /// <summary> Resolves the absolute GUI supervisor manifest lock path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute GUI supervisor manifest lock path. </returns>
    public static string ResolveGuiSupervisorManifestLockPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestLockFileName);
    }

    /// <summary> Resolves the daemon launch-attempts directory under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute daemon launch-attempts directory path. </returns>
    public static string ResolveLaunchAttemptsDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.LaunchAttemptsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one daemon launch-attempt directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="launchAttemptId"> The launch-attempt identifier. </param>
    /// <returns> The absolute daemon launch-attempt directory path. </returns>
    public static string ResolveLaunchAttemptDirectory (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid launchAttemptId)
    {
        return Path.Combine(
            ResolveLaunchAttemptsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(launchAttemptId, nameof(launchAttemptId)));
    }

    /// <summary> Resolves the absolute path to one daemon launch-attempt startup diagnosis file. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="launchAttemptId"> The launch-attempt identifier. </param>
    /// <returns> The absolute daemon launch-attempt startup diagnosis file path. </returns>
    public static string ResolveLaunchAttemptStartupDiagnosisPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid launchAttemptId)
    {
        return Path.Combine(
            ResolveLaunchAttemptDirectory(storageRoot, projectFingerprint, launchAttemptId),
            UcliStoragePathNames.StartupDiagnosisFileName);
    }

    /// <summary> Resolves the uCLI Unity plugin marker cache file under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute plugin marker cache file path. </returns>
    public static string ResolveUnityUcliPluginMarkerCachePath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName);
    }

    /// <summary> Resolves the absolute path to Unity batchmode <c>unity.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute Unity log file path. </returns>
    public static string ResolveUnityLogPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityLogFileName);
    }

    /// <summary> Resolves the plan-token key file under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute plan-token key file path. </returns>
    public static string ResolvePlanTokenKeyPath (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return Path.Combine(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
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

    private static void EnsureSupportedStorageRootLength (string storageRoot)
    {
        var storageRootLength = PathStringNormalizer
            .TrimTrailingDirectorySeparators(storageRoot)
            .Length;
        if (IsWindows()
            && storageRootLength > MaximumSupportedWindowsStorageRootLength)
        {
            throw new PathTooLongException(
                $"Storage root exceeds the {MaximumSupportedWindowsStorageRootLength}-character Windows limit supported by uCLI local storage: {storageRootLength} characters. Move the repository to a shorter path.");
        }
    }

    private static bool IsWindows ()
    {
#if NET8_0_OR_GREATER
        return OperatingSystem.IsWindows();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }

}
