namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation-catalog fetch result. </summary>
internal abstract record OpsCatalogFetchResult
{
    private OpsCatalogFetchResult ()
    {
    }

    /// <summary> Creates a successful fetch result. </summary>
    /// <param name="snapshot"> The validated catalog snapshot. </param>
    /// <returns> The successful result. </returns>
    public static Succeeded Success (OpsCatalogSnapshot snapshot)
    {
        return new Succeeded(snapshot);
    }

    /// <summary> Creates a failed fetch result. </summary>
    /// <param name="failure"> The classified application failure. </param>
    /// <returns> The failed result. </returns>
    public static Failed Failure (ApplicationFailure failure)
    {
        return new Failed(failure);
    }

    /// <summary> Represents a successfully fetched catalog snapshot. </summary>
    internal sealed record Succeeded : OpsCatalogFetchResult
    {
        public Succeeded (OpsCatalogSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public OpsCatalogSnapshot Snapshot { get; }
    }

    /// <summary> Represents a failed catalog fetch. </summary>
    internal sealed record Failed : OpsCatalogFetchResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
