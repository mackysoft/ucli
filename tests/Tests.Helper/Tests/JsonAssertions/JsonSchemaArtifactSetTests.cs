namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;

public sealed class JsonSchemaArtifactSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithReferenceEscapingSchemaRoot_ReturnsReferenceError ()
    {
        using var scope = TestDirectories.CreateTempScope("json-schema-artifact-set", "escaping-reference");
        WriteManifest(scope, "root.schema.json");
        scope.WriteFile(
            "root.schema.json",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://schemas.mackysoft.dev/ucli/v1/root.schema.json",
              "$ref": "../outside.schema.json"
            }
            """);

        using var schemaSet = JsonSchemaArtifactSet.Load(scope.FullPath);
        using var document = JsonDocument.Parse("{}");

        var errors = schemaSet.Validate("root.schema.json", document.RootElement);

        Assert.Contains(
            "schema path '../outside.schema.json' escapes the schema root.",
            errors,
            StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Load_WithUnsupportedKeywordInUnusedDefinition_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-schema-artifact-set", "unused-definition-keyword");
        WriteManifest(scope, "root.schema.json");
        scope.WriteFile(
            "root.schema.json",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://schemas.mackysoft.dev/ucli/v1/root.schema.json",
              "type": "object",
              "$defs": {
                "unused": {
                  "type": "string",
                  "minLength": 1
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => JsonSchemaArtifactSet.Load(scope.FullPath));

        Assert.Contains("unsupported keyword 'minLength'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithSchemaValuedAdditionalProperties_ValidatesAdditionalValues ()
    {
        using var scope = TestDirectories.CreateTempScope("json-schema-artifact-set", "additional-properties-schema");
        WriteManifest(scope, "root.schema.json");
        scope.WriteFile(
            "root.schema.json",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://schemas.mackysoft.dev/ucli/v1/root.schema.json",
              "type": "object",
              "additionalProperties": {
                "type": "string"
              }
            }
            """);
        using var validDocument = JsonDocument.Parse("""{"name":"value"}""");
        using var invalidDocument = JsonDocument.Parse("""{"name":1}""");

        using var schemaSet = JsonSchemaArtifactSet.Load(scope.FullPath);

        Assert.Empty(schemaSet.Validate("root.schema.json", validDocument.RootElement));
        Assert.NotEmpty(schemaSet.Validate("root.schema.json", invalidDocument.RootElement));
    }

    private static void WriteManifest (
        TestDirectoryScope scope,
        string schemaPath)
    {
        scope.WriteFile(
            "schema-manifest.json",
            $$"""
            {
              "schemaSet": "ucli",
              "schemaSetVersion": "v1",
              "protocolVersion": 1,
              "packageVersion": "0.0.0-test",
              "jsonSchemaDialect": "https://json-schema.org/draft/2020-12/schema",
              "schemas": [
                {
                  "$id": "https://schemas.mackysoft.dev/ucli/v1/{{schemaPath}}",
                  "path": "{{schemaPath}}",
                  "kind": "payload",
                  "command": "test"
                }
              ]
            }
            """);
    }
}
