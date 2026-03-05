namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>schemas.catalog.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Entries"> The schema entries. </param>
internal sealed record IndexSchemasCatalogJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? SourceInputsHash,
    IReadOnlyList<IndexSchemaEntryJsonContract>? Entries);