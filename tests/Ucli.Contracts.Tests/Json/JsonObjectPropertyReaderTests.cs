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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("requestId")]
    [InlineData("REQUESTID")]
    [InlineData("RequestId")]
    public void TryGetPropertyIgnoreCase_WhenPropertyExists_ReturnsTrue (
        string propertyName)
    {
        using var document = JsonDocument.Parse("""
            {
              "requestId": "req-1"
            }
            """);

        var result = JsonObjectPropertyReader.TryGetPropertyIgnoreCase(
            document.RootElement,
            propertyName,
            out var property);

        Assert.True(result);
        Assert.Equal("req-1", property.GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPropertyIgnoreCase_WhenPropertyIsMissing_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse("""
            {
              "requestId": "req-1"
            }
            """);

        var result = JsonObjectPropertyReader.TryGetPropertyIgnoreCase(
            document.RootElement,
            "operation",
            out var property);

        Assert.False(result);
        Assert.Equal(default, property);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPropertyIgnoreCase_WhenRootIsNotObject_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse("""["requestId"]""");

        var result = JsonObjectPropertyReader.TryGetPropertyIgnoreCase(
            document.RootElement,
            "requestId",
            out var property);

        Assert.False(result);
        Assert.Equal(default, property);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFindDuplicatePropertyIgnoreCase_WhenObjectContainsDuplicate_ReturnsTrue ()
    {
        using var document = JsonDocument.Parse("""
            {
              "runnerResult": {
                "outputs": [],
                "Outputs": []
              }
            }
            """);

        var result = JsonObjectPropertyReader.TryFindDuplicatePropertyIgnoreCase(
            document.RootElement,
            "$",
            out var duplicatePropertyPath);

        Assert.True(result);
        Assert.Equal("$.runnerResult.Outputs", duplicatePropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFindDuplicatePropertyIgnoreCase_WhenArrayObjectContainsDuplicate_ReturnsTrue ()
    {
        using var document = JsonDocument.Parse("""
            {
              "diagnostics": [
                {
                  "code": "D",
                  "Code": "E"
                }
              ]
            }
            """);

        var result = JsonObjectPropertyReader.TryFindDuplicatePropertyIgnoreCase(
            document.RootElement,
            "$",
            out var duplicatePropertyPath);

        Assert.True(result);
        Assert.Equal("$.diagnostics[0].Code", duplicatePropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFindDuplicatePropertyIgnoreCase_WhenNoDuplicateExists_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse("""
            {
              "requestId": "req-1",
              "operation": "query.assets.find"
            }
            """);

        var result = JsonObjectPropertyReader.TryFindDuplicatePropertyIgnoreCase(
            document.RootElement,
            "$",
            out var duplicatePropertyPath);

        Assert.False(result);
        Assert.Equal(string.Empty, duplicatePropertyPath);
    }
}
