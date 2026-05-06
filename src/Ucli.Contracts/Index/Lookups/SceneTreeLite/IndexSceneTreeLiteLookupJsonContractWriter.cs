using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>scene-tree-lite/&lt;sceneKey&gt;.lookup.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexSceneTreeLiteLookupJsonContractWriter : IndexJsonContractWriterBase<IndexSceneTreeLiteLookupJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexSceneTreeLiteLookupJsonContract contract)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", contract.SchemaVersion);
        writer.WriteString("generatedAtUtc", contract.GeneratedAtUtc);
        WriteNullableString(writer, "scenePath", contract.ScenePath);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "roots", contract.Roots, WriteSceneTreeLiteNode);
        writer.WriteEndObject();
    }

    private static void WriteSceneTreeLiteNode (
        Utf8JsonWriter writer,
        IndexSceneTreeLiteNodeJsonContract node)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", node.Name);
        WriteNullableString(writer, "globalObjectId", node.GlobalObjectId);
        WriteArray(writer, "children", node.Children, WriteSceneTreeLiteNode);
        writer.WriteEndObject();
    }
}
