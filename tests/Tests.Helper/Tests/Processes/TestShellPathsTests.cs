namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

public sealed class TestShellPathsTests
{
    [Theory]
    [InlineData("", "''")]
    [InlineData("plain", "'plain'")]
    [InlineData("has space", "'has space'")]
    [InlineData("it's quoted", "'it'\"'\"'s quoted'")]
    [Trait("Size", "Small")]
    public void QuoteBashArgument_ReturnsSingleQuotedBashArgument (
        string value,
        string expected)
    {
        Assert.Equal(expected, TestShellPaths.QuoteBashArgument(value));
    }
}
