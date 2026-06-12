using System.Text;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryResolveRepositoryRoot_WithGitDirectoryOnCurrentPath_ReturnsCurrentPath ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-repository-root");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", UcliStoragePathNames.GitMarkerName));

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(repositoryRoot);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolveRepositoryRoot_WithGitFileOnParentPath_ReturnsParentRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-repository-root-parent-file");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.WriteFile(
            Path.Combine("Repo", UcliStoragePathNames.GitMarkerName),
            "gitdir: ../.git/worktrees/repo");
        var childPath = scope.CreateDirectory(Path.Combine("Repo", "src", "tool"));

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(childPath);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolveRepositoryRoot_WithoutGitMarker_ReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-repository-root-not-found");
        var directoryPath = scope.CreateDirectory("NoGitRepo");

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(directoryPath);

        Assert.Null(resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveStorageRoot_WithGitMarkerOnParentPath_ReturnsRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-storage-root-parent");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", UcliStoragePathNames.GitMarkerName));
        var startPath = scope.CreateDirectory(Path.Combine("Repo", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveStorageRoot_WithoutGitMarker_ReturnsNormalizedStartPath ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-storage-root-fallback");
        var startPath = scope.CreateDirectory(Path.Combine("Workspace", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(Path.GetFullPath(startPath), resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveConfigPath_ReturnsSharedUcliConfigPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveConfigPath(storageRoot);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.ConfigFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePlanTokenKeyPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolvePlanTokenKeyPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.PlanTokenKeyFileName),
            resolvedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("abc/def")]
    [InlineData("abc\\def")]
    [InlineData("abc:def")]
    public void ResolveFingerprintDirectory_WithUnsafeProjectFingerprint_ThrowsArgumentException (string projectFingerprint)
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        Assert.Throws<ArgumentException>(() =>
        {
            UcliStoragePathResolver.ResolveFingerprintDirectory(storageRoot, projectFingerprint);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUnityUcliPluginMarkerCachePath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveIndexDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexCatalogsDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveIndexCatalogsDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.CatalogsDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTypesCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveTypesCatalogPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.CatalogsDirectoryName,
                UcliStoragePathNames.TypesCatalogFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSchemasCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.CatalogsDirectoryName,
                UcliStoragePathNames.SchemasCatalogFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveOpsCatalogPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.CatalogsDirectoryName,
                UcliStoragePathNames.OpsCatalogFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsDescribePath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");
        var opKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("ucli.go.describe"));

        var resolvedPath = UcliStoragePathResolver.ResolveOpsDescribePath(storageRoot, "abc123", opKey);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.CatalogsDirectoryName,
                UcliStoragePathNames.OpsDescribeDirectoryName,
                opKey + UcliStoragePathNames.OpsDescribeFileExtension),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexLookupsDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveIndexLookupsDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.LookupsDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveAssetSearchLookupPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.LookupsDirectoryName,
                UcliStoragePathNames.AssetSearchLookupFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveGuidPathLookupPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.LookupsDirectoryName,
                UcliStoragePathNames.GuidPathLookupFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.LookupsDirectoryName,
                UcliStoragePathNames.SceneTreeLiteLookupDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupPath_ReturnsHashedSceneScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");
        const string scenePath = @"Assets\Scenes\Sample.unity";
        var expectedSceneKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("Assets/Scenes/Sample.unity"));

        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(storageRoot, "abc123", scenePath);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.LookupsDirectoryName,
                UcliStoragePathNames.SceneTreeLiteLookupDirectoryName,
                expectedSceneKey + UcliStoragePathNames.SceneTreeLiteLookupFileExtension),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveMutationReadPostconditionPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.MutationReadPostconditionFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexInputsManifestPath_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.IndexDirectoryName,
                UcliStoragePathNames.IndexInputsDirectoryName,
                UcliStoragePathNames.IndexInputsManifestFileName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveArtifactsDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.ArtifactsDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTestArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveTestArtifactsDirectory(storageRoot, "abc123");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.ArtifactsDirectoryName,
                UcliStoragePathNames.TestArtifactsDirectoryName),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTestRunArtifactsDirectory_ReturnsRunScopedPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var resolvedPath = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(storageRoot, "abc123", "run-id");

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(storageRoot),
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "abc123",
                UcliStoragePathNames.ArtifactsDirectoryName,
                UcliStoragePathNames.TestArtifactsDirectoryName,
                "run-id"),
            resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunArtifactPaths_ReturnRunScopedArtifactLayout ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");
        var runDirectory = Path.Combine(
            Path.GetFullPath(storageRoot),
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.FingerprintsDirectoryName,
            "abc123",
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.BuildArtifactsDirectoryName,
            "run-id");

        Assert.Equal(
            Path.Combine(runDirectory, UcliStoragePathNames.BuildMetadataFileName),
            UcliStoragePathResolver.ResolveBuildRunMetadataPath(storageRoot, "abc123", "run-id"));
        Assert.Equal(
            Path.Combine(runDirectory, UcliStoragePathNames.BuildReportFileName),
            UcliStoragePathResolver.ResolveBuildRunReportPath(storageRoot, "abc123", "run-id"));
        Assert.Equal(
            Path.Combine(runDirectory, UcliStoragePathNames.BuildLogFileName),
            UcliStoragePathResolver.ResolveBuildRunLogPath(storageRoot, "abc123", "run-id"));
        Assert.Equal(
            Path.Combine(runDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
            UcliStoragePathResolver.ResolveBuildRunOutputManifestPath(storageRoot, "abc123", "run-id"));
        Assert.Equal(
            Path.Combine(runDirectory, UcliStoragePathNames.BuildOutputDirectoryName),
            UcliStoragePathResolver.ResolveBuildRunOutputDirectory(storageRoot, "abc123", "run-id"));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../run-id")]
    [InlineData("..\\run-id")]
    [InlineData("run/id")]
    [InlineData("run\\id")]
    [InlineData("/absolute")]
    [InlineData("C:\\absolute")]
    [InlineData(".")]
    [InlineData("..")]
    public void ResolveTestRunArtifactsDirectory_WithPathSegmentOrTraversalRunId_ThrowsArgumentException (string runId)
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

        var exception = Assert.Throws<ArgumentException>(() =>
            UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(storageRoot, "abc123", runId));

        Assert.Equal("runId", exception.ParamName);
    }
}
