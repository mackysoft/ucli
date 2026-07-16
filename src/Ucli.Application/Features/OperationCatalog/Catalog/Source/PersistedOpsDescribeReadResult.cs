namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents the result of reading one persisted ops describe detail artifact. </summary>
internal sealed record PersistedOpsDescribeReadResult (
    ValidatedOpsOperation? Operation,
    PersistedOpsCatalogReadFailure? ReadFailure)
{
    /// <summary> Gets a value indicating whether reading succeeded. </summary>
    public bool IsSuccess => Operation is not null && ReadFailure is null;

    /// <summary> Creates a successful persisted describe read result. </summary>
    public static PersistedOpsDescribeReadResult Success (ValidatedOpsOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new PersistedOpsDescribeReadResult(operation, null);
    }

    /// <summary> Creates a failed persisted describe read result. </summary>
    public static PersistedOpsDescribeReadResult Failure (PersistedOpsCatalogReadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new PersistedOpsDescribeReadResult(null, failure);
    }
}
