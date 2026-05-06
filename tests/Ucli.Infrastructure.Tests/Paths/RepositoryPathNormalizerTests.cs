using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class RepositoryPathNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithRepositoryRoot_ReturnsDotRelativePath ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer");

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, repositoryRoot);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(repositoryRoot), result.FullPath);
        Assert.Equal(".", result.RepositoryRelativeSlashPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithRelativePath_ResolvesFromRepositoryRoot ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer");

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, Path.Combine("ProjectSettings", "TestSettings.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(Path.Combine(repositoryRoot, "ProjectSettings", "TestSettings.json")), result.FullPath);
        Assert.Equal("ProjectSettings/TestSettings.json", result.RepositoryRelativeSlashPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithPathOutsideRepositoryRoot_ReturnsOutsideRepositoryRootFailure ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer", "repo");
        var outsidePath = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer", "outside");

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, outsidePath);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathNormalizationFailureKind.OutsideRepositoryRoot, result.FailureKind);
        Assert.Null(result.FullPath);
        Assert.Null(result.RepositoryRelativeSlashPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithTraversalOutsideRepositoryRoot_ReturnsOutsideRepositoryRootFailure ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer", "repo");

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, Path.Combine("..", "outside"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PathNormalizationFailureKind.OutsideRepositoryRoot, result.FailureKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithMixedSeparators_ReturnsSlashSeparatedRelativePath ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer");
        var pathValue = $"Assets{Path.DirectorySeparatorChar}Scenes{Path.AltDirectorySeparatorChar}Main.unity";

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, pathValue);

        Assert.True(result.IsSuccess);
        Assert.Equal("Assets/Scenes/Main.unity", result.RepositoryRelativeSlashPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WithInvalidPathFormat_ReturnsInvalidFormatFailure ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-repository-path-normalizer");

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, "invalid\0path");

        Assert.False(result.IsSuccess);
        Assert.Equal(PathNormalizationFailureKind.InvalidFormat, result.FailureKind);
        Assert.Null(result.FullPath);
        Assert.Null(result.RepositoryRelativeSlashPath);
    }
}
