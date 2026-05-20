namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one validated operation-catalog snapshot. </summary>
internal sealed record OpsCatalogSnapshot
{
    private OpsCatalogSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        GeneratedAtUtc = generatedAtUtc;
        Operations = operations.ToArray();
    }

    /// <summary> Gets the catalog generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the validated operation entries. </summary>
    public IReadOnlyList<IndexOpEntryJsonContract> Operations { get; }

    /// <summary> Creates a snapshot when the entry collection satisfies the public operation-entry contract. </summary>
    /// <param name="generatedAtUtc"> The catalog generation timestamp copied into the snapshot. </param>
    /// <param name="operations"> The operation entries to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="propertyName"> The non-empty property name used in validation errors. </param>
    /// <param name="snapshot"> The validated snapshot when the method returns <see langword="true" />; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error when the method returns <see langword="false" />; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when every entry is valid for a persisted public catalog; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentException"> <paramref name="propertyName" /> is null, empty, or whitespace. </exception>
    public static bool TryCreate (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract>? operations,
        string propertyName,
        out OpsCatalogSnapshot? snapshot,
        out string? error)
    {
        return TryCreate(
            generatedAtUtc,
            operations,
            propertyName,
            allowEditLoweringOnlyEntries: false,
            out snapshot,
            out error);
    }

    /// <summary> Creates a snapshot when the entry collection satisfies the selected operation-entry contract. </summary>
    /// <param name="generatedAtUtc"> The catalog generation timestamp copied into the snapshot. </param>
    /// <param name="operations"> The operation entries to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="propertyName"> The non-empty property name used in validation errors. </param>
    /// <param name="allowEditLoweringOnlyEntries"> <see langword="true" /> to allow edit-lowering-only entries in addition to public entries. </param>
    /// <param name="snapshot"> The validated snapshot when the method returns <see langword="true" />; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error when the method returns <see langword="false" />; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when every entry is valid for the selected catalog contract; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentException"> <paramref name="propertyName" /> is null, empty, or whitespace. </exception>
    public static bool TryCreate (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract>? operations,
        string propertyName,
        bool allowEditLoweringOnlyEntries,
        out OpsCatalogSnapshot? snapshot,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (!IndexCatalogContractValidator.TryValidateOpsEntries(
                operations,
                propertyName,
                allowEditLoweringOnlyEntries,
                out error))
        {
            snapshot = null;
            return false;
        }

        snapshot = new OpsCatalogSnapshot(generatedAtUtc, operations!);
        error = null;
        return true;
    }
}
