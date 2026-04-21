namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>guid-path.lookup.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Entries"> The GUID-path entries. </param>
internal sealed record IndexGuidPathLookupJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? SourceInputsHash,
    IReadOnlyList<IndexGuidPathEntryJsonContract>? Entries);