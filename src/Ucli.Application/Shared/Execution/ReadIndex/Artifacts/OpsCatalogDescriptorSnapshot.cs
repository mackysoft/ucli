using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated lightweight operation-catalog descriptor snapshot. </summary>
internal sealed record OpsCatalogDescriptorSnapshot : IReadIndexArtifactSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private OpsCatalogDescriptorSnapshot (
        DateTimeOffset generatedAtUtc,
        Sha256Digest sourceInputsHash,
        IReadOnlyList<ValidatedOpsCatalogEntry> entries)
    {
        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        Entries = entries;
    }

    /// <inheritdoc />
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <inheritdoc />
    public Sha256Digest SourceInputsHash { get; }

    /// <summary> Gets the validated lightweight operation descriptor entries. </summary>
    public IReadOnlyList<ValidatedOpsCatalogEntry> Entries { get; }

    /// <summary>
    /// Determines whether another snapshot represents the same catalog publication, including its ordered describe references.
    /// </summary>
    /// <param name="other"> The validated catalog snapshot to compare. </param>
    /// <returns>
    /// <see langword="true" /> when the publication time, source-input digest, and every validated descriptor are equal;
    /// otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="other" /> is <see langword="null" />. </exception>
    public bool IsSameGenerationAs (OpsCatalogDescriptorSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (GeneratedAtUtc != other.GeneratedAtUtc
            || SourceInputsHash != other.SourceInputsHash
            || Entries.Count != other.Entries.Count)
        {
            return false;
        }

        for (var i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != other.Entries[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary> Projects a persisted catalog contract when all persisted values are canonical and valid. </summary>
    /// <param name="contract"> The raw persisted catalog contract. </param>
    /// <param name="snapshot"> The validated snapshot when projection succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when projection succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        IndexOpsCatalogJsonContract? contract,
        [NotNullWhen(true)]
        out OpsCatalogDescriptorSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.SourceInputsHash, out var sourceInputsHash)
            || !TryCreateEntries(contract.Entries, out var entries))
        {
            return false;
        }

        snapshot = new OpsCatalogDescriptorSnapshot(
            contract.GeneratedAtUtc,
            sourceInputsHash,
            entries);
        return true;
    }

    private static bool TryCreateEntries (
        IReadOnlyList<IndexOpsCatalogEntryJsonContract>? entries,
        [NotNullWhen(true)] out IReadOnlyList<ValidatedOpsCatalogEntry>? validatedEntries)
    {
        validatedEntries = null;
        if (entries == null)
        {
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        var describeKeys = new HashSet<Sha256Digest>();
        var projectedEntries = new ValidatedOpsCatalogEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!TryCreateEntry(entry, out var projectedEntry)
                || !operationNames.Add(projectedEntry.Name)
                || !describeKeys.Add(projectedEntry.DescribeKey))
            {
                return false;
            }

            projectedEntries[i] = projectedEntry;
        }

        validatedEntries = Array.AsReadOnly(projectedEntries);
        return true;
    }

    private static bool TryCreateEntry (
        IndexOpsCatalogEntryJsonContract? entry,
        [NotNullWhen(true)] out ValidatedOpsCatalogEntry? projectedEntry)
    {
        projectedEntry = null;
        if (entry == null
            || string.IsNullOrWhiteSpace(entry.Name)
            || string.IsNullOrWhiteSpace(entry.Description)
            || !ContractLiteralCodec.TryParse<UcliOperationKind>(entry.Kind, out var kind)
            || !ContractLiteralCodec.TryParse<OperationPolicy>(entry.Policy, out var policy)
            || !Sha256Digest.TryParse(entry.DescribeKey, out var describeKey)
            || !Sha256Digest.TryParse(entry.DescribeHash, out var describeHash))
        {
            return false;
        }

        projectedEntry = new ValidatedOpsCatalogEntry(
            entry.Name,
            kind,
            policy,
            entry.Description,
            describeKey,
            describeHash);
        return true;
    }
}
