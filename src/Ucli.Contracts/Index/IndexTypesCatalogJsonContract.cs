namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>types.catalog.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Entries"> The type entries. </param>
internal sealed record IndexTypesCatalogJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? SourceInputsHash,
    IReadOnlyList<IndexTypeEntryJsonContract>? Entries);