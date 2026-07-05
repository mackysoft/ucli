using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void SplitCommaSeparatedValues_WithCommaSeparatedValue_ReturnsTrimmedEntries ()
    {
        var values = TestRunCommand.SplitCommaSeparatedValues(
            "MyGame.Tests.EditMode, MyGame.Tests.PlayMode");

        Assert.NotNull(values);
        Assert.Equal(
            ["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"],
            values);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SplitCommaSeparatedValues_WithNull_ReturnsNull ()
    {
        var values = TestRunCommand.SplitCommaSeparatedValues(null);

        Assert.Null(values);
    }
}
