using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class PresentationStaticSchemaContractTests
{
    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("common.pixel-dimensions")]
    [InlineData("cli-output.payload.screenshot.game.ok")]
    [InlineData("cli-output.payload.screenshot.scene.ok")]
    public void PixelDimensionsSchemas_RequirePositiveWidthAndHeight (
        string logicalName)
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find(logicalName));
        var dimensions = FindObjectDefinition(artifact.Document, "width");
        var properties = dimensions.GetProperty("properties");
        var required = dimensions.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("width", required);
        Assert.Contains("height", required);
        Assert.Equal(1, properties.GetProperty("width").GetProperty("minimum").GetInt32());
        Assert.Equal(1, properties.GetProperty("height").GetProperty("minimum").GetInt32());
        Assert.False(dimensions.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void UnityProjectColorSpaceSchema_DeclaresTheSharedVocabulary ()
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find("common.unity-project-color-space"));
        var literals = artifact.Document.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();

        Assert.Equal(["gamma", "linear"], literals);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("cli-output.payload.screenshot.game.ok")]
    [InlineData("cli-output.payload.screenshot.scene.ok")]
    public void ScreenshotSchemas_UseTheSharedPresentationProperties (
        string logicalName)
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find(logicalName));
        var capture = FindObjectDefinition(artifact.Document, "requestedDimensions");
        var properties = capture.GetProperty("properties");

        Assert.True(properties.TryGetProperty("dimensions", out _));
        Assert.True(properties.TryGetProperty("projectColorSpace", out _));
        Assert.False(properties.TryGetProperty("requestedWidth", out _));
        Assert.False(properties.TryGetProperty("requestedHeight", out _));
        Assert.False(properties.TryGetProperty("colorSpace", out _));
    }

    private static UcliStaticSchemaSet LoadSchemaSet () =>
        UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));

    private static JsonElement FindObjectDefinition (
        JsonElement document,
        string propertyName)
    {
        return document.GetProperty("$defs")
            .EnumerateObject()
            .Select(static definition => definition.Value)
            .Single(definition =>
                definition.TryGetProperty("properties", out var properties)
                && properties.TryGetProperty(propertyName, out _));
    }
}
