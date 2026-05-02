using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationJsonSchemaGeneratorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenPropertyAttributesArePresent_EmitsRequiredDescriptionAndConstraints ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(SampleArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var root = document.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("Sample args.", root.GetProperty("description").GetString());
        Assert.Equal("name", root.GetProperty("required")[0].GetString());

        var nameProperty = root.GetProperty("properties").GetProperty("name");
        Assert.Equal("string", nameProperty.GetProperty("type").GetString());
        Assert.Equal("Stable sample name.", nameProperty.GetProperty("description").GetString());
        Assert.Equal(1, nameProperty.GetProperty("minLength").GetInt32());

        var countProperty = root.GetProperty("properties").GetProperty("count");
        Assert.Equal("integer", countProperty.GetProperty("type")[0].GetString());
        Assert.Equal("null", countProperty.GetProperty("type")[1].GetString());
        Assert.Equal(0, countProperty.GetProperty("minimum").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateArgsSchemaJson_WhenOneOfRequiredAttributesArePresent_EmitsSelectorAlternatives ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(UcliOperationContracts.ResolveSelectorArgs));

        using var document = JsonDocument.Parse(schemaJson);
        var root = document.RootElement;
        Assert.Equal(6, root.GetProperty("oneOf").GetArrayLength());
        Assert.Equal("componentType", root.GetProperty("allOf")[0].GetProperty("if").GetProperty("required")[0].GetString());
        Assert.Equal("scene", root.GetProperty("allOf")[0].GetProperty("then").GetProperty("oneOf")[0].GetProperty("required")[0].GetString());
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
    public void FindMissingPropertyDescriptions_WhenDescriptionIsMissing_ReturnsJsonPropertyName ()
    {
        var missing = UcliOperationJsonSchemaGenerator.FindMissingPropertyDescriptions(typeof(MissingDescriptionArgs));

        Assert.Equal(new[] { "rawName" }, missing);
    }

    [UcliDescription("Sample args.")]
    private sealed record SampleArgs (
        [property: UcliRequired]
        [property: UcliDescription("Stable sample name.")]
        [property: UcliMinLength(1)]
        string Name,

        [property: UcliDescription("Optional sample count.")]
        [property: UcliMinimum(0)]
        int? Count);

    private sealed record MissingDescriptionArgs (
        [property: JsonPropertyName("rawName")]
        string Name);
}
