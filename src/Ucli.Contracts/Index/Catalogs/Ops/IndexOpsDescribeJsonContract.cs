namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one persisted <c>ops.describe/&lt;opKey&gt;.json</c> detail contract. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Operation"> The full operation detail entry. </param>
internal sealed record IndexOpsDescribeJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? SourceInputsHash,
    IndexOpEntryJsonContract? Operation);
