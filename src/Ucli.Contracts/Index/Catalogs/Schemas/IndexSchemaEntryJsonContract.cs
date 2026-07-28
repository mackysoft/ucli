using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one schema entry contract in <c>schemas.catalog.json</c>. </summary>
[Description("Serialized object schema entry.")]
internal sealed record IndexSchemaEntryJsonContract
{
    [JsonConstructor]
    public IndexSchemaEntryJsonContract (
        string? SchemaKey,
        string? Kind,
        string? TypeId,
        string? DisplayName,
        IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? Properties)
    {
        this.SchemaKey = SchemaKey;
        this.Kind = Kind;
        this.TypeId = TypeId;
        this.DisplayName = DisplayName;
        this.Properties = Properties;
    }

    /// <summary> Gets the schema-key value, for example <c>comp:&lt;typeId&gt;</c>. </summary>
    [JsonRequired]
    [Description("Schema key, such as comp:<typeId> or asset:<typeId>.")]
    public string? SchemaKey { get; init; }

    /// <summary> Gets the schema-kind literal value. </summary>
    [JsonRequired]
    [Description("Schema kind literal.")]
    public string? Kind { get; init; }

    /// <summary> Gets the stable type identifier value. </summary>
    [JsonRequired]
    [Description("Stable Unity type identifier.")]
    public string? TypeId { get; init; }

    /// <summary> Gets the display-name value. </summary>
    [JsonRequired]
    [Description("Display name for the type.")]
    public string? DisplayName { get; init; }

    /// <summary> Gets the schema property entries. </summary>
    [JsonRequired]
    [Description("Serialized properties exposed by this schema.")]
    public IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? Properties { get; init; }
}
