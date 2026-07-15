namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one validated operation-catalog snapshot. </summary>
internal sealed record OpsCatalogSnapshot
{
    internal OpsCatalogSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ValidatedOpsOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (generatedAtUtc == default || generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Catalog generation timestamp must be a non-default UTC value.", nameof(generatedAtUtc));
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        var copiedOperations = new ValidatedOpsOperation[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i]
                ?? throw new ArgumentException("Operation collection must not contain null values.", nameof(operations));
            if (!operationNames.Add(operation.Name))
            {
                throw new ArgumentException($"Operation entry '{operation.Name}' is duplicated.", nameof(operations));
            }

            copiedOperations[i] = operation;
        }

        GeneratedAtUtc = generatedAtUtc;
        Operations = Array.AsReadOnly(copiedOperations);
    }

    /// <summary> Gets the catalog generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the validated operation entries. </summary>
    public IReadOnlyList<ValidatedOpsOperation> Operations { get; }

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

        if (generatedAtUtc == default || generatedAtUtc.Offset != TimeSpan.Zero)
        {
            snapshot = null;
            error = "Catalog generation timestamp must be a non-default UTC value.";
            return false;
        }

        if (!IndexCatalogContractValidator.TryProjectOpsEntries(
                operations,
                propertyName,
                allowEditLoweringOnlyEntries,
                requireCanonicalLiterals: false,
                out var validatedOperations,
                out error))
        {
            snapshot = null;
            return false;
        }

        snapshot = new OpsCatalogSnapshot(generatedAtUtc, validatedOperations);
        error = null;
        return true;
    }
}
