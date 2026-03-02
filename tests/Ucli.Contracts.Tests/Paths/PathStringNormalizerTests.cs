using MackySoft.Ucli.Contracts.Paths;

namespace MackySoft.Ucli.Contracts.Tests.Paths;

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
    public void TrimTrailingDirectorySeparators_RemovesTrailingDirectorySeparators ()
    {
        var value = $"path{Path.DirectorySeparatorChar}{Path.AltDirectorySeparatorChar}";

        var result = PathStringNormalizer.TrimTrailingDirectorySeparators(value);

        Assert.Equal("path", result);
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
    public void TrimTrailingDirectorySeparators_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathStringNormalizer.TrimTrailingDirectorySeparators(null!);
        });
    }
}