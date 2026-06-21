using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class PathStringNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ToSlashSeparated_ReplacesBackslashesWithSlashes ()
    {
        var result = PathStringNormalizer.ToSlashSeparated(@"a\b\c");

        Assert.Equal("a/b/c", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToPlatformSeparated_ReplacesKnownSeparatorsWithPlatformSeparator ()
    {
        var result = PathStringNormalizer.ToPlatformSeparated(@"a\b/c");

        var expected = $"a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}c";
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReplaceAltSeparatorWithPlatformSeparator_ReplacesOnlyAltSeparators ()
    {
        var value = @"a\b/c";

        var result = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(value);

        var expected = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimTrailingDirectorySeparators_RemovesTrailingDirectorySeparators ()
    {
        var value = $"path{Path.DirectorySeparatorChar}{Path.AltDirectorySeparatorChar}";

        var result = PathStringNormalizer.TrimTrailingDirectorySeparators(value);

        Assert.Equal("path", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathRoot_WithCurrentRoot_ReturnsTrue ()
    {
        var rootPath = Path.GetPathRoot(Path.GetFullPath("."))!;

        var result = PathStringNormalizer.IsPathRoot(rootPath);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathRoot_WithChildPath_ReturnsFalse ()
    {
        var childPath = Path.GetFullPath(Path.Combine("root-check", "child"));

        var result = PathStringNormalizer.IsPathRoot(childPath);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeAbsolutePathForStableIdentity_TrimsTrailingSeparatorsWithoutTrimmingRoot ()
    {
        var fullPath = Path.GetFullPath(Path.Combine("stable-identity-root", "child"));
        var pathWithTrailingSeparator = fullPath + Path.DirectorySeparatorChar;

        var result = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(pathWithTrailingSeparator);

        Assert.Equal(PathStringNormalizer.NormalizeCaseForCurrentPlatform(fullPath), result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeAbsolutePathForStableIdentity_PreservesRootPath ()
    {
        var rootPath = Path.GetPathRoot(Path.GetFullPath("."))!;

        var result = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(rootPath);

        Assert.Equal(PathStringNormalizer.NormalizeCaseForCurrentPlatform(rootPath), result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToSlashSeparated_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.ToSlashSeparated(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToPlatformSeparated_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.ToPlatformSeparated(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReplaceAltSeparatorWithPlatformSeparator_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimTrailingDirectorySeparators_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.TrimTrailingDirectorySeparators(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathRoot_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.IsPathRoot(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NormalizeAbsolutePathForStableIdentity_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(null!);
        });
    }
}
