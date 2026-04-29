using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents the result of reading one persisted ops catalog. </summary>
/// <param name="Entries"> The persisted ops entries on success; otherwise <see langword="null" />. </param>
/// <param name="GeneratedAtUtc"> The persisted generation timestamp on success; otherwise <see langword="null" />. </param>
/// <param name="Freshness"> The observed persisted freshness on success; otherwise <see langword="null" />. </param>
/// <param name="ErrorCode"> The machine-readable failure code on failure; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The user-facing failure message on failure; otherwise <see langword="null" />. </param>
internal sealed record PersistedOpsCatalogReadResult (
    IReadOnlyList<IndexOpEntryJsonContract>? Entries,
    DateTimeOffset? GeneratedAtUtc,
    IndexFreshness? Freshness,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether reading succeeded. </summary>
    public bool IsSuccess => Entries is not null
        && GeneratedAtUtc.HasValue
        && Freshness.HasValue
        && string.IsNullOrWhiteSpace(ErrorCode)
        && string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary> Creates a successful persisted-catalog read result. </summary>
    /// <param name="entries"> The persisted ops entries. </param>
    /// <param name="generatedAtUtc"> The persisted generation timestamp. </param>
    /// <param name="freshness"> The observed persisted freshness. </param>
    /// <returns> The successful read result. </returns>
    public static PersistedOpsCatalogReadResult Success (
        IReadOnlyList<IndexOpEntryJsonContract> entries,
        DateTimeOffset generatedAtUtc,
        IndexFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new PersistedOpsCatalogReadResult(
            Entries: entries,
            GeneratedAtUtc: generatedAtUtc,
            Freshness: freshness,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary> Creates a failed persisted-catalog read result. </summary>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="errorMessage"> The user-facing failure message. </param>
    /// <returns> The failed read result. </returns>
    public static PersistedOpsCatalogReadResult Failure (
        string errorCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new PersistedOpsCatalogReadResult(
            Entries: null,
            GeneratedAtUtc: null,
            Freshness: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}
