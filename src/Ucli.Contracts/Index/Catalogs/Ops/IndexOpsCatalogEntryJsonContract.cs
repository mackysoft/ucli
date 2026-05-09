namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one lightweight operation descriptor stored in <c>ops.catalog.json</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation kind value. </param>
/// <param name="Policy"> The operation policy value. </param>
/// <param name="DescribeKey"> The opaque key for the matching describe artifact. </param>
/// <param name="DescribeHash"> The SHA-256 lower-hex hash of the matching describe artifact JSON. </param>
internal sealed record IndexOpsCatalogEntryJsonContract (
    string? Name,
    string? Kind,
    string? Policy,
    string? DescribeKey,
    string? DescribeHash);
