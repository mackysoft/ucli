namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Represents one index-catalog write result with optional failure reason. </summary>
/// <param name="IsSuccess"> Whether write operation succeeded. </param>
/// <param name="ErrorMessage"> The failure reason when write failed; otherwise <see langword="null" />. </param>
internal sealed record IndexCatalogWriteResult (
    bool IsSuccess,
    string? ErrorMessage)
{
    /// <summary> Creates one successful write result. </summary>
    /// <returns> The successful result. </returns>
    public static IndexCatalogWriteResult Success ()
    {
        return new IndexCatalogWriteResult(true, null);
    }

    /// <summary> Creates one failed write result. </summary>
    /// <param name="errorMessage"> The failure reason text. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="errorMessage" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static IndexCatalogWriteResult Failure (string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message must not be empty.", nameof(errorMessage));
        }

        return new IndexCatalogWriteResult(false, errorMessage);
    }
}
