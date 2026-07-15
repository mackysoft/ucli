using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated persisted GUID-path lookup snapshot. </summary>
internal sealed record GuidPathLookupSnapshot : IReadIndexArtifactSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private GuidPathLookupSnapshot (
        DateTimeOffset generatedAtUtc,
        Sha256Digest sourceInputsHash,
        IReadOnlyList<GuidPathLookupEntry> entries)
    {
        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        Entries = entries;
    }

    /// <inheritdoc />
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <inheritdoc />
    public Sha256Digest SourceInputsHash { get; }

    /// <summary> Gets the validated GUID-path entries. </summary>
    public IReadOnlyList<GuidPathLookupEntry> Entries { get; }

    /// <summary> Projects a persisted GUID-path contract when its values are valid. </summary>
    public static bool TryCreate (
        IndexGuidPathLookupJsonContract? contract,
        [NotNullWhen(true)]
        out GuidPathLookupSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.SourceInputsHash, out var sourceInputsHash)
            || !IndexCatalogContractValidator.TryProjectGuidPathEntries(
                contract.Entries,
                "entries",
                out var entries,
                out _))
        {
            return false;
        }

        snapshot = new GuidPathLookupSnapshot(
            contract.GeneratedAtUtc,
            sourceInputsHash,
            entries);
        return true;
    }
}
