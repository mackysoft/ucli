namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one schema entry contract in <c>schemas.catalog.json</c>. </summary>
/// <param name="SchemaKey"> The schema-key value (for example <c>comp:&lt;typeId&gt;</c>). </param>
/// <param name="Kind"> The schema-kind literal value. </param>
/// <param name="TypeId"> The stable type identifier value. </param>
/// <param name="DisplayName"> The display-name value. </param>
/// <param name="Properties"> The schema property entries. </param>
internal sealed record IndexSchemaEntryJsonContract (
    string? SchemaKey,
    string? Kind,
    string? TypeId,
    string? DisplayName,
    IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? Properties);