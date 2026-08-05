using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Presentation;

public sealed class PixelDimensionsTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Constructor_WhenDimensionIsNotPositive_ThrowsArgumentOutOfRangeException (
        int width,
        int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelDimensions(width, height));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_UsesWidthAndHeightValues ()
    {
        var dimensions = new PixelDimensions(1919, 1079);

        Assert.Equal(dimensions, new PixelDimensions(1919, 1079));
        Assert.NotEqual(dimensions, new PixelDimensions(1920, 1079));
        Assert.NotEqual(dimensions, new PixelDimensions(1919, 1080));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonSerialization_RoundTripsTheClosedValueShape ()
    {
        var dimensions = new PixelDimensions(1919, 1079);

        var json = JsonSerializer.Serialize(
            dimensions,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<PixelDimensions>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(dimensions, roundTripped);
        Assert.Equal("{\"width\":1919,\"height\":1079}", json);
    }
}
