namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one ops-catalog source refresh result. </summary>
internal abstract record OpsCatalogSourceRefreshResult
{
    private OpsCatalogSourceRefreshResult ()
    {
    }

    /// <summary> Creates a successful source refresh result. </summary>
    public static Succeeded Success (OpsCatalogSnapshot snapshot, string? fallbackReason)
    {
        return new Succeeded(snapshot, fallbackReason);
    }

    /// <summary> Creates a failed source refresh result. </summary>
    public static Failed Failure (ApplicationFailure failure)
    {
        return new Failed(failure);
    }

    /// <summary> Represents a successfully refreshed catalog snapshot. </summary>
    internal sealed record Succeeded : OpsCatalogSourceRefreshResult
    {
        public Succeeded (
            OpsCatalogSnapshot snapshot,
            string? fallbackReason)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            FallbackReason = fallbackReason;
        }

        public OpsCatalogSnapshot Snapshot { get; }

        public string? FallbackReason { get; }
    }

    /// <summary> Represents a failed source refresh. </summary>
    internal sealed record Failed : OpsCatalogSourceRefreshResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
