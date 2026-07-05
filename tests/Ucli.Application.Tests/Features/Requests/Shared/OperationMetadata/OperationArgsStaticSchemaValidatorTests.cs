namespace MackySoft.Ucli.Application.Tests;

using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

public sealed class OperationArgsStaticSchemaValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRefIsCircular_ReturnsSchemaInvalid ()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "node": {
                  "$ref": "#/$defs/Node"
                }
              },
              "$defs": {
                "Node": {
                  "$ref": "#/$defs/Node"
                }
              }
            }
            """;
        using var argsDocument = JsonDocument.Parse("""{"node":{}}""");

        var isValid = OperationArgsStaticSchemaValidator.TryValidate(
            schemaJson,
            argsDocument.RootElement,
            out var schemaInvalid,
            out var error);

        Assert.False(isValid);
        Assert.True(schemaInvalid);
        Assert.Contains("circular reference", error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenArgsPropertyIsDuplicated_ReturnsInvalid ()
    {
        const string schemaJson = """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "path": {
                  "type": "string"
                }
              }
            }
            """;
        using var argsDocument = JsonDocument.Parse("""{"path":"Assets/A.unity","path":"Assets/B.unity"}""");

        var isValid = OperationArgsStaticSchemaValidator.TryValidate(
            schemaJson,
            argsDocument.RootElement,
            out var schemaInvalid,
            out var error);

        Assert.False(isValid);
        Assert.False(schemaInvalid);
        Assert.Equal("Property 'args.path' is duplicated.", error);
    }
}
