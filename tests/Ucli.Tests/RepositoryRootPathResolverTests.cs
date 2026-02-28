using MackySoft.Tests;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests;

public sealed class RepositoryRootPathResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithGitDirectoryOnCurrentPath_ReturnsCurrentPath ()
    {
        using var scope = TestDirectories.CreateTempScope("repository-root-resolver", "git-directory-current");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", ".git"));

        var resolvedPath = RepositoryRootPathResolver.TryResolve(repositoryRoot);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithGitFileOnParentPath_ReturnsParentRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("repository-root-resolver", "git-file-parent");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.WriteFile(Path.Combine("Repo", ".git"), "gitdir: ../.git/worktrees/repo");
        var childPath = scope.CreateDirectory(Path.Combine("Repo", "src", "tool"));

        var resolvedPath = RepositoryRootPathResolver.TryResolve(childPath);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryResolve_WithoutGitMarker_ReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("repository-root-resolver", "not-found");
        var directoryPath = scope.CreateDirectory("NoGitRepo");

        var resolvedPath = RepositoryRootPathResolver.TryResolve(directoryPath);

        Assert.Null(resolvedPath);
    }
}