using System.Diagnostics.CodeAnalysis;
using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Resolves repository-root and shared <c>.ucli</c> storage paths. </summary>
public static class UcliStoragePathResolver
{
    /// <summary> Tries to resolve a repository root from a guarded starting directory. </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="startPath" /> is <see langword="null" />.
    /// </exception>
    public static AbsolutePath? TryResolveRepositoryRoot (AbsolutePath startPath)
    {
        if (startPath is null)
        {
            throw new ArgumentNullException(nameof(startPath));
        }

        return TryResolveRepositoryRootCore(startPath);
    }

    /// <summary> Resolves storage root from a starting directory path. </summary>
    /// <param name="startPath"> The guarded starting-directory path. </param>
    /// <returns> The repository root when found; otherwise the normalized absolute <paramref name="startPath" />. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="startPath" /> is <see langword="null" />.
    /// </exception>
    public static AbsolutePath ResolveStorageRoot (AbsolutePath startPath)
    {
        if (startPath is null)
        {
            throw new ArgumentNullException(nameof(startPath));
        }

        var repositoryRoot = TryResolveRepositoryRootCore(startPath);
        if (repositoryRoot is not null)
        {
            return repositoryRoot;
        }

        // NOTE:
        // Local and CI environments may not have a Git repository.
        // Use the starting path as a deterministic fallback storage root.
        return startPath;
    }

    private static AbsolutePath? TryResolveRepositoryRootCore (AbsolutePath startPath)
    {
        if (!Directory.Exists(startPath.Value))
        {
            return null;
        }

        var directoryPath = startPath;
        while (true)
        {
            var markerPath = ContainedPath.Create(
                directoryPath,
                RootRelativePath.Parse(UcliStoragePathNames.GitMarkerName)).Target;
            if (Directory.Exists(markerPath.Value) || File.Exists(markerPath.Value))
            {
                return directoryPath;
            }

            if (!directoryPath.TryGetParent(out var parentDirectory))
            {
                return null;
            }

            directoryPath = parentDirectory;
        }
    }

    /// <summary> Resolves the guarded path to the <c>.ucli</c> directory. </summary>
    public static AbsolutePath ResolveUcliDirectoryPath (AbsolutePath storageRoot)
    {
        return ResolveUnderStorageRoot(storageRoot, UcliStoragePathNames.UcliDirectoryName);
    }

    /// <summary> Resolves the guarded path to shared <c>.ucli/config.json</c>. </summary>
    public static AbsolutePath ResolveConfigPath (AbsolutePath storageRoot)
    {
        return ResolveUnderStorageRoot(
            storageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.ConfigFileName);
    }

    /// <summary> Resolves the absolute path to the <c>.ucli/local</c> directory. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <returns> The absolute <c>.ucli/local</c> directory path. </returns>
    public static AbsolutePath ResolveLocalDirectoryPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveUcliDirectoryPath(storageRoot),
            UcliStoragePathNames.LocalDirectoryName);
    }

    /// <summary> Tries to resolve the shared <c>.ucli</c> and <c>.ucli/local</c> roots that own a directory path. </summary>
    /// <param name="directoryPath"> The directory path that may be under <c>.ucli/local</c>. </param>
    /// <param name="ucliDirectoryPath"> The resolved shared <c>.ucli</c> directory path when matched. </param>
    /// <param name="localDirectoryPath"> The resolved shared <c>.ucli/local</c> directory path when matched. </param>
    /// <returns> <see langword="true" /> when <paramref name="directoryPath" /> is under <c>.ucli/local</c>; otherwise <see langword="false" />. </returns>
    internal static bool TryResolveLocalStorageRootDirectories (
        AbsolutePath directoryPath,
        [NotNullWhen(true)] out AbsolutePath? ucliDirectoryPath,
        [NotNullWhen(true)] out AbsolutePath? localDirectoryPath)
    {
        var currentPath = directoryPath;
        while (currentPath.TryGetParent(out var parentPath))
        {
            if (parentPath.TryGetParent(out var storagePath))
            {
                var expectedUcliPath = ContainedPath.Create(
                    storagePath,
                    RootRelativePath.Parse(UcliStoragePathNames.UcliDirectoryName)).Target;
                var expectedLocalPath = ContainedPath.Create(
                    parentPath,
                    RootRelativePath.Parse(UcliStoragePathNames.LocalDirectoryName)).Target;
                if (parentPath.IsSameAs(expectedUcliPath)
                    && currentPath.IsSameAs(expectedLocalPath))
                {
                    ucliDirectoryPath = parentPath;
                    localDirectoryPath = currentPath;
                    return true;
                }
            }

            currentPath = parentPath;
        }

        ucliDirectoryPath = null;
        localDirectoryPath = null;
        return false;
    }

    /// <summary> Resolves the absolute path to the <c>.ucli/local/supervisor</c> directory. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <returns> The absolute supervisor runtime directory path. </returns>
    public static AbsolutePath ResolveSupervisorDirectoryPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveLocalDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorDirectoryName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>manifest.json</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor manifest file path. </returns>
    public static AbsolutePath ResolveSupervisorManifestPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorManifestFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>manifest.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor manifest mutation lock file path. </returns>
    public static AbsolutePath ResolveSupervisorManifestLockPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorManifestLockFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>bootstrap.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor bootstrap lock file path. </returns>
    public static AbsolutePath ResolveSupervisorBootstrapLockPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorBootstrapLockFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>runtime-ownership.lock</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor runtime ownership lock file path. </returns>
    public static AbsolutePath ResolveSupervisorRuntimeOwnershipLockPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorRuntimeOwnershipLockFileName);
    }

    /// <summary> Resolves the absolute path to supervisor <c>supervisor.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute supervisor log file path. </returns>
    public static AbsolutePath ResolveSupervisorLogPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorLogFileName);
    }

    /// <summary> Resolves the absolute path to the launch-agent plist used for supervisor bootstrap. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The absolute launch-agent plist path. </returns>
    public static AbsolutePath ResolveSupervisorLaunchAgentPlistPath (AbsolutePath storageRoot)
    {
        return ResolveUnderPath(
            ResolveSupervisorDirectoryPath(storageRoot),
            UcliStoragePathNames.SupervisorLaunchAgentPlistFileName);
    }

    /// <summary> Resolves one guarded project-scoped storage directory. </summary>
    public static AbsolutePath ResolveProjectDirectory (
        AbsolutePath storageRoot,
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
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index directory path. </returns>
    public static AbsolutePath ResolveIndexDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.IndexDirectoryName);
    }

    /// <summary> Resolves the absolute read-index writer lock path for one project fingerprint. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index writer lock path. </returns>
    public static AbsolutePath ResolveReadIndexWriteLockPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexWriteLockFileName);
    }

    /// <summary> Resolves the atomic pointer to the current immutable read-index generation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute current-generation pointer path. </returns>
    public static AbsolutePath ResolveReadIndexCurrentGenerationPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexCurrentGenerationFileName);
    }

    /// <summary> Resolves the directory containing immutable read-index generations. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute generation-root directory path. </returns>
    public static AbsolutePath ResolveReadIndexGenerationsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName);
    }

    /// <summary> Resolves one immutable read-index generation directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute immutable generation directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    public static AbsolutePath ResolveReadIndexGenerationDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexGenerationsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves the directory containing unpublished read-index generations. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute staging-root directory path. </returns>
    internal static AbsolutePath ResolveReadIndexStagingDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexStagingDirectoryName);
    }

    /// <summary> Resolves one unpublished read-index generation directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute staging generation directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    internal static AbsolutePath ResolveReadIndexStagingGenerationDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexStagingDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves the directory containing generation-retention markers. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute retention-marker directory path. </returns>
    internal static AbsolutePath ResolveReadIndexRetentionDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexRetentionDirectoryName);
    }

    /// <summary> Resolves the deletion-eligibility marker for one immutable generation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty generation identifier. </param>
    /// <returns> The absolute retention-marker path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    internal static AbsolutePath ResolveReadIndexRetentionMarkerPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexRetentionDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(generationId, nameof(generationId)));
    }

    /// <summary> Resolves one read-index catalogs directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/index/catalogs</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index catalogs directory path. </returns>
    public static AbsolutePath ResolveIndexCatalogsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CatalogsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index types catalog file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index types catalog file path. </returns>
    public static AbsolutePath ResolveTypesCatalogPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TypesCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index schemas catalog file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index schemas catalog file path. </returns>
    public static AbsolutePath ResolveSchemasCatalogPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexCatalogsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.SchemasCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index ops catalog file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index ops catalog file path. </returns>
    public static AbsolutePath ResolveOpsCatalogPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.OpsCatalogFileName);
    }

    /// <summary> Resolves the absolute path to one read-index ops describe artifact directory. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index ops describe artifact directory path. </returns>
    public static AbsolutePath ResolveOpsDescribeDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexOpsDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index ops describe artifact file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="opKey"> The operation describe content digest. </param>
    /// <returns> The absolute read-index ops describe artifact file path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> or <paramref name="opKey" /> is <see langword="null" />. </exception>
    public static AbsolutePath ResolveOpsDescribePath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Sha256Digest opKey)
    {
        if (opKey == null)
        {
            throw new ArgumentNullException(nameof(opKey));
        }

        return ResolveUnderPath(
            ResolveOpsDescribeDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeSha256Digest(opKey)
                + UcliStoragePathNames.OpsDescribeFileExtension);
    }

    /// <summary> Resolves the absolute path to one read-index asset-search lookup file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index asset-search lookup file path. </returns>
    public static AbsolutePath ResolveAssetSearchLookupPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.AssetSearchLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index GUID-path lookup file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index GUID-path lookup file path. </returns>
    public static AbsolutePath ResolveGuidPathLookupPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.GuidPathLookupFileName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup directory. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup directory path. </returns>
    public static AbsolutePath ResolveSceneTreeLiteLookupDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveIndexDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ReadIndexScenesDirectoryName);
    }

    /// <summary> Resolves the absolute path to one read-index scene-tree-lite lookup file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="scenePath"> The normalized project-relative Unity scene asset path. </param>
    /// <returns> The absolute read-index scene-tree-lite lookup file path. </returns>
    public static AbsolutePath ResolveSceneTreeLiteLookupPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        SceneAssetPath scenePath)
    {
        if (scenePath is null)
        {
            throw new ArgumentNullException(nameof(scenePath));
        }

        // SceneAssetPath owns the portable Unity asset-path identity. This resolver only
        // maps that already-guarded product identity into the uCLI storage layout.
        var sceneKey = Sha256Digest.Compute(Encoding.UTF8.GetBytes(scenePath.Value));
        return ResolveUnderPath(
            ResolveSceneTreeLiteLookupDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeSha256Digest(sceneKey)
                + UcliStoragePathNames.SceneTreeLiteLookupFileExtension);
    }

    /// <summary> Resolves the absolute path to one read-index inputs manifest file. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="generationId"> The non-empty immutable generation identifier. </param>
    /// <returns> The absolute read-index inputs manifest file path. </returns>
    public static AbsolutePath ResolveIndexInputsManifestPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        return ResolveUnderPath(
            ResolveReadIndexGenerationDirectory(storageRoot, projectFingerprint, generationId),
            UcliStoragePathNames.IndexInputsManifestFileName);
    }

    /// <summary> Resolves one artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped artifacts directory path. </returns>
    public static AbsolutePath ResolveArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    /// <summary> Resolves one work directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/work</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped work directory path. </returns>
    public static AbsolutePath ResolveWorkDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.WorkDirectoryName);
    }

    /// <summary> Resolves one test-artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/test</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped test-artifacts directory path. </returns>
    public static AbsolutePath ResolveTestArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.TestArtifactsDirectoryName);
    }

    /// <summary> Resolves one compile-artifacts directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/compile</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute project-scoped compile-artifacts directory path. </returns>
    public static AbsolutePath ResolveCompileArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.CompileArtifactsDirectoryName);
    }

    /// <summary> Resolves the project-scoped screenshot artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute screenshot artifact directory path. </returns>
    public static AbsolutePath ResolveScreenshotArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveArtifactsDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ScreenshotDirectoryName);
    }

    /// <summary> Resolves one capture-scoped screenshot artifact directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute capture artifact directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static AbsolutePath ResolveScreenshotCaptureArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return ResolveUnderPath(
            ResolveScreenshotArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(captureId, nameof(captureId)));
    }

    /// <summary> Resolves one final screenshot PNG artifact path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute screenshot PNG artifact path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static AbsolutePath ResolveScreenshotCaptureArtifactPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return ResolveUnderPath(
            ResolveScreenshotCaptureArtifactsDirectory(storageRoot, projectFingerprint, captureId),
            UcliStoragePathNames.ScreenshotPngFileName);
    }

    /// <summary> Resolves the project-scoped screenshot work directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute screenshot work directory path. </returns>
    public static AbsolutePath ResolveScreenshotWorkDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveWorkDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.ScreenshotDirectoryName);
    }

    /// <summary> Resolves one capture-scoped screenshot staging directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute capture staging directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static AbsolutePath ResolveScreenshotCaptureStagingDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return ResolveUnderPath(
            ResolveScreenshotWorkDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(captureId, nameof(captureId)));
    }

    /// <summary> Resolves one normalized raw screenshot staging file path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="captureId"> The non-empty capture identifier. </param>
    /// <returns> The absolute raw screenshot staging file path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="captureId" /> is empty. </exception>
    public static AbsolutePath ResolveScreenshotCaptureRawStagingPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid captureId)
    {
        return ResolveUnderPath(
            ResolveScreenshotCaptureStagingDirectory(storageRoot, projectFingerprint, captureId),
            UcliStoragePathNames.ScreenshotRawStagingFileName);
    }

    /// <summary> Resolves the guarded mutation read-postcondition file path. </summary>
    public static AbsolutePath ResolveMutationReadPostconditionPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.MutationReadPostconditionFileName)).Target;
    }

    /// <summary> Resolves the guarded mutation read-postcondition writer lock path. </summary>
    public static AbsolutePath ResolveMutationReadPostconditionLockPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.MutationReadPostconditionFileName + ".lock")).Target;
    }

    /// <summary> Resolves one test-run directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/test/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute test-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public static AbsolutePath ResolveTestRunArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid runId)
    {
        return ResolveUnderPath(
            ResolveTestArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId)));
    }

    /// <summary> Resolves one compile-run directory under <c>.ucli/local/projects/&lt;projectStorageKey&gt;/artifacts/compile/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute compile-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public static AbsolutePath ResolveCompileRunArtifactsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid runId)
    {
        return ResolveUnderPath(
            ResolveCompileArtifactsDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId)));
    }

    /// <summary> Resolves one build-run directory under <c>.ucli/local/build-runs/&lt;runStorageKey&gt;</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute build-run storage directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public static AbsolutePath ResolveBuildRunDirectory (
        AbsolutePath storageRoot,
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
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute build-run artifacts directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public static AbsolutePath ResolveBuildRunArtifactsDirectory (
        AbsolutePath storageRoot,
        Guid runId)
    {
        return ResolveUnderPath(
            ResolveBuildRunDirectory(storageRoot, runId),
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    /// <summary> Resolves one runner output directory under <c>.ucli/local/build-runs/&lt;runStorageKey&gt;/work/output</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="runId"> The non-empty run identifier. </param>
    /// <returns> The absolute runner output directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public static AbsolutePath ResolveBuildRunOutputDirectory (
        AbsolutePath storageRoot,
        Guid runId)
    {
        return ResolveUnderPath(
            ResolveBuildRunDirectory(storageRoot, runId),
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    /// <summary> Resolves the guarded daemon session file path. </summary>
    public static AbsolutePath ResolveSessionPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.SessionFileName)).Target;
    }

    /// <summary> Resolves the absolute oneshot bootstrap-envelope directory for one project fingerprint. </summary>
    public static AbsolutePath ResolveOneshotBootstrapDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.OneshotBootstrapDirectoryName);
    }

    /// <summary> Resolves the absolute path for one non-empty oneshot bootstrap identifier. </summary>
    public static AbsolutePath ResolveOneshotBootstrapPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid bootstrapId)
    {
        return ResolveUnderPath(
            ResolveOneshotBootstrapDirectory(storageRoot, projectFingerprint),
            StoragePathSegmentCodec.EncodeGuid(bootstrapId, nameof(bootstrapId))
                + UcliStoragePathNames.OneshotBootstrapFileExtension);
    }

    /// <summary> Resolves the guarded daemon session-generation lock path. </summary>
    public static AbsolutePath ResolveDaemonSessionLockPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.DaemonSessionLockFileName)).Target;
    }

    /// <summary> Resolves the guarded daemon diagnosis file path. </summary>
    public static AbsolutePath ResolveDaemonDiagnosisPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.DaemonDiagnosisFileName)).Target;
    }

    /// <summary> Resolves the guarded daemon lifecycle observation file path. </summary>
    public static AbsolutePath ResolveDaemonLifecyclePath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.DaemonLifecycleFileName)).Target;
    }

    /// <summary> Resolves the guarded daemon lifecycle observation lock path. </summary>
    public static AbsolutePath ResolveDaemonLifecycleLockPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.DaemonLifecycleFileName + ".lock")).Target;
    }

    /// <summary> Resolves the absolute path to GUI supervisor <c>gui-supervisor.json</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute GUI supervisor manifest path. </returns>
    public static AbsolutePath ResolveGuiSupervisorManifestPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestFileName);
    }

    /// <summary> Resolves the absolute GUI supervisor manifest lock path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute GUI supervisor manifest lock path. </returns>
    public static AbsolutePath ResolveGuiSupervisorManifestLockPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.GuiSupervisorManifestLockFileName);
    }

    /// <summary> Resolves the guarded daemon launch-attempts directory. </summary>
    public static AbsolutePath ResolveLaunchAttemptsDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ContainedPath.Create(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliStoragePathNames.LaunchAttemptsDirectoryName)).Target;
    }

    /// <summary> Resolves the guarded path to one daemon launch-attempt directory. </summary>
    public static AbsolutePath ResolveLaunchAttemptDirectory (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid launchAttemptId)
    {
        return ContainedPath.Create(
            ResolveLaunchAttemptsDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(
                StoragePathSegmentCodec.EncodeGuid(launchAttemptId, nameof(launchAttemptId)))).Target;
    }

    /// <summary> Resolves the guarded startup diagnosis path for one daemon launch attempt. </summary>
    public static AbsolutePath ResolveLaunchAttemptStartupDiagnosisPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid launchAttemptId)
    {
        return ContainedPath.Create(
            ResolveLaunchAttemptDirectory(storageRoot, projectFingerprint, launchAttemptId),
            RootRelativePath.Parse(UcliStoragePathNames.StartupDiagnosisFileName)).Target;
    }

    /// <summary> Resolves the uCLI Unity plugin marker cache file under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute plugin marker cache file path. </returns>
    public static AbsolutePath ResolveUnityUcliPluginMarkerCachePath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName);
    }

    /// <summary> Resolves the absolute path to Unity batchmode <c>unity.log</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute Unity log file path. </returns>
    public static AbsolutePath ResolveUnityLogPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.UnityLogFileName);
    }

    /// <summary> Resolves the plan-token key file under one project-scoped directory. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The absolute plan-token key file path. </returns>
    public static AbsolutePath ResolvePlanTokenKeyPath (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        return ResolveUnderPath(
            ResolveProjectDirectory(storageRoot, projectFingerprint),
            UcliStoragePathNames.PlanTokenKeyFileName);
    }

    private static AbsolutePath ResolveUnderStorageRoot (
        AbsolutePath storageRoot,
        params string[] relativeSegments)
    {
        return ResolveUnderPath(storageRoot, relativeSegments);
    }

    private static AbsolutePath ResolveUnderPath (
        AbsolutePath directoryPath,
        params string[] relativeSegments)
    {
        var relativePath = RootRelativePath.Parse(Path.Combine(relativeSegments));
        return ContainedPath.Create(directoryPath, relativePath).Target;
    }

}
