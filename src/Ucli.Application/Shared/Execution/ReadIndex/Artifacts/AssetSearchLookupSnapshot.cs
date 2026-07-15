using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated persisted asset-search lookup snapshot. </summary>
internal sealed record AssetSearchLookupSnapshot : IReadIndexArtifactSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private AssetSearchLookupSnapshot (
        DateTimeOffset generatedAtUtc,
        Sha256Digest sourceInputsHash,
        IReadOnlyList<AssetSearchLookupEntry> entries)
    {
        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        Entries = entries;
    }

    /// <inheritdoc />
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <inheritdoc />
    public Sha256Digest SourceInputsHash { get; }

    /// <summary> Gets the validated asset-search entries. </summary>
    public IReadOnlyList<AssetSearchLookupEntry> Entries { get; }

    /// <summary> Projects a persisted asset-search contract when its values are valid. </summary>
    public static bool TryCreate (
        IndexAssetSearchLookupJsonContract? contract,
        [NotNullWhen(true)]
        out AssetSearchLookupSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.SourceInputsHash, out var sourceInputsHash)
            || !IndexCatalogContractValidator.TryProjectAssetSearchEntries(
                contract.Entries,
                "entries",
                out var entries,
                out _))
        {
            return false;
        }

        snapshot = new AssetSearchLookupSnapshot(
            contract.GeneratedAtUtc,
            sourceInputsHash,
            entries);
        return true;
    }
}
