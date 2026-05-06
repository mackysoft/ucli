using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Tests.Json;

public sealed class JsonObjectPropertyReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FindUnknownProperty_ReturnsUnknownPropertyName_WhenObjectContainsUnknownProperty ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
            requestId = "req-1",
            unknown = true,
        });
        var allowedPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
        };

        var result = JsonObjectPropertyReader.FindUnknownProperty(jsonObject, allowedPropertyNames);

        Assert.Equal("unknown", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindUnknownProperty_ReturnsNull_WhenAllPropertiesAreAllowed ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
            requestId = "req-1",
        });
        var allowedPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
        };

        var result = JsonObjectPropertyReader.FindUnknownProperty(jsonObject, allowedPropertyNames);

        Assert.Null(result);
    }
}
