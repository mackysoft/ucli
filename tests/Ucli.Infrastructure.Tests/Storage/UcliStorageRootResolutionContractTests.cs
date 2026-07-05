using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStorageRootResolutionContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void TryResolveRepositoryRoot_WithGitDirectoryOnCurrentPath_ReturnsCurrentPath ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-repository-root");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", UcliStoragePathNames.GitMarkerName));

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(repositoryRoot);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
    public void TryResolveRepositoryRoot_WithoutGitMarker_ReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-repository-root-not-found");
        var directoryPath = scope.CreateDirectory("NoGitRepo");

        var resolvedPath = UcliStoragePathResolver.TryResolveRepositoryRoot(directoryPath);

        Assert.Null(resolvedPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
    public void ResolveStorageRoot_WithoutGitMarker_ReturnsNormalizedStartPath ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "resolve-storage-root-fallback");
        var startPath = scope.CreateDirectory(Path.Combine("Workspace", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(Path.GetFullPath(startPath), resolvedPath);
    }
}
