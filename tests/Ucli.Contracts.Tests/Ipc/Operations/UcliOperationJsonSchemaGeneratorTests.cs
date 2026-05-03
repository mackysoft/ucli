using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationJsonSchemaGeneratorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenPropertyAttributesArePresent_EmitsStructureOnly ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(SampleArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var root = document.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("name", root.GetProperty("required")[0].GetString());
        Assert.False(root.TryGetProperty("description", out _));
        Assert.False(root.TryGetProperty("minProperties", out _));

        var nameProperty = root.GetProperty("properties").GetProperty("name");
        Assert.Equal("string", nameProperty.GetProperty("type").GetString());
        Assert.False(nameProperty.TryGetProperty("description", out _));
        Assert.False(nameProperty.TryGetProperty("minLength", out _));

        var countProperty = root.GetProperty("properties").GetProperty("count");
        Assert.Equal("integer", countProperty.GetProperty("type")[0].GetString());
        Assert.Equal("null", countProperty.GetProperty("type")[1].GetString());
        Assert.False(countProperty.TryGetProperty("minimum", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenSelectorAttributesArePresent_DoesNotEmitCompositionKeywords ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(ResolveSelectorArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("oneOf", out _));
        Assert.False(root.TryGetProperty("allOf", out _));
        Assert.False(root.TryGetProperty("if", out _));
        Assert.False(root.TryGetProperty("then", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateResultSchemaJson_WhenResultIsNoResult_ReturnsNull ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateResultSchemaJson(typeof(UcliNoResult));

        Assert.Null(schemaJson);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateResultSchemaJson_WhenResultContainsRecursiveArray_EmitsDefsAndRefs ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateResultSchemaJson(typeof(SceneTreeResult));

        using var document = JsonDocument.Parse(schemaJson!);
        var root = document.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());

        var rootsItems = root
            .GetProperty("properties")
            .GetProperty("roots")
            .GetProperty("items");

        var nodeRef = "#/$defs/" + nameof(IndexSceneTreeLiteNodeJsonContract);
        Assert.Equal(nodeRef, rootsItems.GetProperty("$ref").GetString());

        var nodeSchema = root
            .GetProperty("$defs")
            .GetProperty(nameof(IndexSceneTreeLiteNodeJsonContract));

        Assert.Equal("object", nodeSchema.GetProperty("type").GetString());
        Assert.False(nodeSchema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(nodeRef, nodeSchema
            .GetProperty("properties")
            .GetProperty("children")
            .GetProperty("items")
            .GetProperty("$ref")
            .GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindMissingPropertyDescriptions_WhenDescriptionIsMissing_ReturnsJsonPropertyName ()
    {
        var missing = UcliOperationJsonSchemaGenerator.FindMissingPropertyDescriptions(typeof(MissingDescriptionArgs));

        Assert.Equal(new[] { "rawName" }, missing);
    }

    [UcliDescription("Sample args.")]
    private sealed record SampleArgs (
        [property: UcliRequired]
        [property: UcliDescription("Stable sample name.")]
        string Name,

        [property: UcliDescription("Optional sample count.")]
        int? Count);

    private sealed record MissingDescriptionArgs (
        [property: JsonPropertyName("rawName")]
        string Name);
}
