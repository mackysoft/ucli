using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class UcliStoragePathResolverContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryResolveRepositoryRoot_WithGitDirectoryOnCurrentPath_ReturnsCurrentPath ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "resolve-repository-root");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", UcliStoragePathNames.GitMarkerName));

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(repositoryRoot);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveStorageRoot_WithoutGitMarker_ReturnsNormalizedStartPath ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "resolve-storage-root-fallback");
        var startPath = scope.CreateDirectory(Path.Combine("Workspace", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(Path.GetFullPath(startPath), resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveConfigPath_ReturnsSharedUcliConfigPath ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-contracts-storage-root");

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
        var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-contracts-storage-root");

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
}