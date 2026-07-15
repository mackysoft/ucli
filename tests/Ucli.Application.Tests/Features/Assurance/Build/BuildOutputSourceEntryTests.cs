using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildOutputSourceEntryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("relative/output")]
    [Trait("Size", "Small")]
    public void FromAbsolutePath_WhenPathIsNotFullyQualified_Throws (string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => BuildOutputSourceEntry.FromAbsolutePath(path!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromAbsolutePath_WhenPathContainsRelativeSegments_StoresNormalizedPath ()
    {
        var path = Path.Combine(Path.GetTempPath(), "ucli-build-source", "nested", "..", "player");

        var source = BuildOutputSourceEntry.FromAbsolutePath(path);

        var absolute = Assert.IsType<BuildOutputSourceEntry.Absolute>(source);
        Assert.Equal(Path.GetFullPath(path), absolute.Path);
    }
}
