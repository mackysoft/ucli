using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class AbsolutePathArgumentParserAttributeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsRelative_ResolvesAgainstCurrentDirectory ()
    {
        var parsed = AbsolutePathArgumentParserAttribute.TryParse("UnityProject", out var projectPath);

        Assert.True(parsed);
        Assert.NotNull(projectPath);
        Assert.True(projectPath.IsSameAs(AbsolutePath.Parse(Path.GetFullPath("UnityProject"))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueHasInvalidPathSyntax_ReturnsFalse ()
    {
        var parsed = AbsolutePathArgumentParserAttribute.TryParse("invalid\0path", out var projectPath);

        Assert.False(parsed);
        Assert.Null(projectPath);
    }
}
