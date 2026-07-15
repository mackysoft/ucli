using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcExecuteStepIdTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithNullValue_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecuteStepId(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" step")]
    [InlineData("step ")]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidText_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteStepId(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonRoundTrip_PreservesStringWireRepresentation ()
    {
        var value = new IpcExecuteStepId("step-1");

        var json = JsonSerializer.Serialize(value);
        var deserialized = JsonSerializer.Deserialize<IpcExecuteStepId>(json);

        Assert.Equal("\"step-1\"", json);
        Assert.Equal(value, deserialized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToString_ReturnsWireValue ()
    {
        var value = new IpcExecuteStepId("step-1");

        Assert.Equal("step-1", value.ToString());
    }
}
