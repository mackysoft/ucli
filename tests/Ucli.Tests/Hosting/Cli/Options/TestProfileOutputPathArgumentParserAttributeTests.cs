using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class TestProfileOutputPathArgumentParserAttributeTests
{
    [Theory]
    [InlineData("profile", "profile.json")]
    [InlineData("profile.txt", "profile.txt.json")]
    [InlineData("profile.JSON", "profile.JSON")]
    public void TryParse_NormalizesTheProfileFileName (string value, string expected)
    {
        var parsed = TestProfileOutputPathArgumentParserAttribute.TryParse(value, out var outputPath);

        Assert.True(parsed);
        Assert.Equal(
            AbsolutePath.Resolve(AbsolutePath.Parse(Environment.CurrentDirectory), expected),
            outputPath);
    }

    [Theory]
    [InlineData("profiles/")]
    [InlineData("profiles\\")]
    public void TryParse_WithDirectoryStylePath_RejectsTheValue (string value)
    {
        var parsed = TestProfileOutputPathArgumentParserAttribute.TryParse(value, out var outputPath);

        Assert.False(parsed);
        Assert.Null(outputPath);
    }
}
