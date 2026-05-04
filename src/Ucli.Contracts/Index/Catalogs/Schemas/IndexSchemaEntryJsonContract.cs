using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one schema entry contract in <c>schemas.catalog.json</c>. </summary>
[UcliDescription("Serialized object schema entry.")]
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
    [UcliRequired]
    [UcliDescription("Schema key, such as comp:<typeId> or asset:<typeId>.")]
    public string? SchemaKey { get; init; }

    /// <summary> Gets the schema-kind literal value. </summary>
    [UcliRequired]
    [UcliDescription("Schema kind literal.")]
    public string? Kind { get; init; }

    /// <summary> Gets the stable type identifier value. </summary>
    [UcliRequired]
    [UcliDescription("Stable Unity type identifier.")]
    public string? TypeId { get; init; }

    /// <summary> Gets the display-name value. </summary>
    [UcliRequired]
    [UcliDescription("Display name for the type.")]
    public string? DisplayName { get; init; }

    /// <summary> Gets the schema property entries. </summary>
    [UcliRequired]
    [UcliDescription("Serialized properties exposed by this schema.")]
    public IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? Properties { get; init; }
}
