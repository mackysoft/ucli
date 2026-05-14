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

        var optionalTextProperty = root.GetProperty("properties").GetProperty("optionalText");
        Assert.Equal("string", optionalTextProperty.GetProperty("type").GetString());

        var explicitNullTextProperty = root.GetProperty("properties").GetProperty("explicitNullText");
        Assert.Equal("string", explicitNullTextProperty.GetProperty("type")[0].GetString());
        Assert.Equal("null", explicitNullTextProperty.GetProperty("type")[1].GetString());
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
    public void CreateArgsSchemaJson_WhenPropertyUsesSemanticStringValue_EmitsStringStructure ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(ScenePathArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var pathProperty = document.RootElement
            .GetProperty("properties")
            .GetProperty("path");

        Assert.Equal("string", pathProperty.GetProperty("type").GetString());
        Assert.False(pathProperty.TryGetProperty("properties", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenReferenceUsesSemanticStringValues_OmitsRequestLocalAliasBranch ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(AssetReferenceArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var properties = document.RootElement.GetProperty("properties");
        Assert.False(properties.TryGetProperty("var", out _));
        Assert.Equal("string", properties.GetProperty("assetGuid").GetProperty("type").GetString());
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
        var required = root.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).ToArray();

        var nodeRef = "#/$defs/" + nameof(IndexSceneTreeLiteNodeJsonContract);
        Assert.Equal(nodeRef, rootsItems.GetProperty("$ref").GetString());
        Assert.Contains("sourceState", required);
        Assert.Contains("window", required);

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
        Assert.Contains(
            nodeSchema.GetProperty("required").EnumerateArray(),
            item => item.GetString() == "childrenState");
        Assert.Equal("string", nodeSchema
            .GetProperty("properties")
            .GetProperty("childrenState")
            .GetProperty("type")
            .GetString());
        var windowProperties = root
            .GetProperty("properties")
            .GetProperty("window")
            .GetProperty("properties");
        Assert.True(windowProperties.TryGetProperty("cursor", out _));
        Assert.True(windowProperties.TryGetProperty("nextCursor", out _));
        Assert.False(windowProperties.TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenBoundedRawQueryArgsContainWindowInputs_EmitsLimitAndCursor ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(AssetsFindArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var properties = document.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("limit", out var limitProperty));
        Assert.Equal("integer", limitProperty.GetProperty("type")[0].GetString());
        Assert.Equal("null", limitProperty.GetProperty("type")[1].GetString());
        Assert.True(properties.TryGetProperty("cursor", out var cursorProperty));
        Assert.Equal("string", cursorProperty.GetProperty("type").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenCsEvalArgsContainsSourceOnly_RequiresSourceOnly ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(CsEvalArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var root = document.RootElement;
        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("source", out _));
        Assert.False(properties.TryGetProperty("entryPoint", out _));

        var required = root.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(new[] { "source" }, required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateResultSchemaJson_WhenCsEvalResultContainsJsonAnyValue_EmitsStructuralResultSchema ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateResultSchemaJson(typeof(CsEvalResult));

        using var document = JsonDocument.Parse(schemaJson!);
        var root = document.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(root.GetProperty("required").EnumerateArray(), item => item.GetString() == "sourceDigest");
        Assert.Contains(root.GetProperty("required").EnumerateArray(), item => item.GetString() == "compile");
        Assert.DoesNotContain(root.GetProperty("required").EnumerateArray(), item => item.GetString() == "sourceKind");
        Assert.DoesNotContain(root.GetProperty("required").EnumerateArray(), item => item.GetString() == "resolvedEntryPoint");
        Assert.True(root.GetProperty("properties").TryGetProperty("sourceKind", out _));
        Assert.True(root.GetProperty("properties").TryGetProperty("resolvedEntryPoint", out _));

        var returnValueProperty = root
            .GetProperty("properties")
            .GetProperty("returnValue");

        Assert.Equal("object", returnValueProperty.GetProperty("type").GetString());
        Assert.False(returnValueProperty.GetProperty("additionalProperties").GetBoolean());

        var serializedValueSchema = returnValueProperty
            .GetProperty("properties")
            .GetProperty("value");

        Assert.Equal(JsonValueKind.Object, serializedValueSchema.ValueKind);
        Assert.Empty(serializedValueSchema.EnumerateObject());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindMissingPropertyDescriptions_WhenDescriptionIsMissing_ReturnsJsonPropertyName ()
    {
        var missing = UcliOperationJsonSchemaGenerator.FindMissingPropertyDescriptions(typeof(MissingDescriptionArgs));

        Assert.Equal(new[] { "rawName" }, missing);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindMissingPropertyDescriptions_WhenSemanticStringValueHasDescription_DoesNotReportProperty ()
    {
        var missing = UcliOperationJsonSchemaGenerator.FindMissingPropertyDescriptions(typeof(ScenePathArgs));

        Assert.Empty(missing);
    }

    [UcliDescription("Sample args.")]
    private sealed record SampleArgs (
        [property: UcliRequired]
        [property: UcliDescription("Stable sample name.")]
        string Name,

        [property: UcliDescription("Optional sample count.")]
        int? Count,

        [property: UcliDescription("Optional text omitted when absent.")]
        string? OptionalText,

        [property: UcliJsonAllowNull]
        [property: UcliDescription("Optional text emitted as null when explicitly unknown.")]
        string? ExplicitNullText);

    private sealed record MissingDescriptionArgs (
        [property: JsonPropertyName("rawName")]
        string Name);
}
