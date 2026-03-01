using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class UcliStorageRootPathResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithGitMarkerOnParentPath_ReturnsRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-storage-root-resolver", "git-parent");
        var repositoryRoot = scope.CreateDirectory("Repo");
        scope.CreateDirectory(Path.Combine("Repo", ".git"));
        var startPath = scope.CreateDirectory(Path.Combine("Repo", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(repositoryRoot, resolvedPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutGitMarker_ReturnsNormalizedStartPath ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-storage-root-resolver", "fallback-start-path");
        var startPath = scope.CreateDirectory(Path.Combine("Workspace", "UnityProject"));

        var resolvedPath = UcliStoragePathResolver.ResolveStorageRoot(startPath);

        Assert.Equal(Path.GetFullPath(startPath), resolvedPath);
    }
}