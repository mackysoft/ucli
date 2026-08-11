using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class FilePathReferenceArgumentParserAttributeTests
{
    [Fact]
    public void TryParse_WithRootRelativePath_PreservesTheReference ()
    {
        var parsed = FilePathReferenceArgumentParserAttribute.TryParse(
            "profiles/verify.json",
            out var path);

        Assert.True(parsed);
        Assert.Equal("profiles/verify.json", path!.ToString());
    }

    [Fact]
    public void TryParse_WithAbsolutePath_PreservesTheReference ()
    {
        var expected = AbsolutePath.Parse(Path.GetFullPath("verify.json"));

        var parsed = FilePathReferenceArgumentParserAttribute.TryParse(
            expected.Value,
            out var path);

        Assert.True(parsed);
        Assert.Equal(expected.Value, path!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../verify.json")]
    [InlineData("invalid\0path")]
    public void TryParse_WithInvalidReference_RejectsTheValue (string value)
    {
        var parsed = FilePathReferenceArgumentParserAttribute.TryParse(value, out var path);

        Assert.False(parsed);
        Assert.Null(path);
    }
}
