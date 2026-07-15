using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class PathNormalizerTests
{
    private static readonly string?[] RelativeOrEmptyPathValues =
    [
        null,
        string.Empty,
        "   ",
        "relative/path",
    ];

    private static readonly string?[] EmptyPathValues =
    [
        null,
        string.Empty,
        " ",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WithEmptyPath_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(
            static () => FullPathNormalizationResult.Success(string.Empty));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WithNoneKind_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => FullPathNormalizationResult.Failure(PathNormalizationFailureKind.None, "failure"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsFullyQualifiedPath_WithFullPath_ReturnsTrue ()
    {
        var path = Path.GetFullPath("src");

        var result = PathNormalizer.IsFullyQualifiedPath(path);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsFullyQualifiedPath_WithRelativeOrEmptyPath_ReturnsFalse ()
    {
        foreach (var path in RelativeOrEmptyPathValues)
        {
            var result = PathNormalizer.IsFullyQualifiedPath(path);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeFullPath_WithRelativePath_ReturnsFullPath ()
    {
        var result = PathNormalizer.TryNormalizeFullPath("src");

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath("src"), result.FullPath);
        Assert.Equal(PathNormalizationFailureKind.None, result.FailureKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeFullPath_WithRelativePathAndBasePath_UsesBasePath ()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ucli-path-normalizer");

        var result = PathNormalizer.TryNormalizeFullPath(Path.Combine("ProjectSettings", "TestSettings.json"), basePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(Path.Combine(basePath, "ProjectSettings", "TestSettings.json")), result.FullPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeFullPath_WithEmptyPath_ReturnsEmptyPathFailure ()
    {
        foreach (var pathValue in EmptyPathValues)
        {
            var result = PathNormalizer.TryNormalizeFullPath(pathValue);

            Assert.False(result.IsSuccess);
            Assert.Equal(PathNormalizationFailureKind.EmptyPath, result.FailureKind);
            Assert.Null(result.FullPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeFullPath_WithInvalidPathFormat_ReturnsInvalidFormatFailure ()
    {
        var result = PathNormalizer.TryNormalizeFullPath("invalid\0path");

        Assert.False(result.IsSuccess);
        Assert.Equal(PathNormalizationFailureKind.InvalidFormat, result.FailureKind);
        Assert.Null(result.FullPath);
    }
}
