using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class BuildRunnerOutputPathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathUsesBackslashes_NormalizesToPortableRelativePath ()
    {
        var path = new BuildRunnerOutputPath(@"reports\build-report.json");

        Assert.Equal("reports/build-report.json", path.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" player/Player")]
    [InlineData("player/Player ")]
    [InlineData("/player/Player")]
    [InlineData("C:/player/Player")]
    [InlineData(".")]
    [InlineData("./player/Player")]
    [InlineData("player/../Player")]
    [InlineData("player//Player")]
    [InlineData("player/Player/")]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsNotPortableRelative_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BuildRunnerOutputPath(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new BuildRunnerOutputPath(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenPathIsInvalid_ReturnsFalseWithoutValue ()
    {
        var succeeded = BuildRunnerOutputPath.TryParse("../player", out var path);

        Assert.False(succeeded);
        Assert.Null(path);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonRoundTrip_PreservesStringWireShapeAndTypedValue ()
    {
        var path = new BuildRunnerOutputPath("reports/build-report.json");

        var json = System.Text.Json.JsonSerializer.Serialize(path, IpcJsonSerializerOptions.Default);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<BuildRunnerOutputPath>(
            json,
            IpcJsonSerializerOptions.Default);

        Assert.Equal("\"reports/build-report.json\"", json);
        Assert.Equal(path, roundTrip);
    }
}
