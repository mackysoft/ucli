namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>ops.catalog.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Entries"> The lightweight operation descriptor entries. </param>
internal sealed record IndexOpsCatalogJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? SourceInputsHash,
    IReadOnlyList<IndexOpsCatalogEntryJsonContract>? Entries);
