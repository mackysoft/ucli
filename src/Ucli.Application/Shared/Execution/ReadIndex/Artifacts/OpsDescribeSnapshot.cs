using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated persisted operation detail snapshot. </summary>
internal sealed record OpsDescribeSnapshot : IReadIndexArtifactSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private OpsDescribeSnapshot (
        DateTimeOffset generatedAtUtc,
        Sha256Digest sourceInputsHash,
        ValidatedOpsOperation operation)
    {
        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        Operation = operation;
    }

    /// <inheritdoc />
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <inheritdoc />
    public Sha256Digest SourceInputsHash { get; }

    /// <summary> Gets the validated operation detail. </summary>
    public ValidatedOpsOperation Operation { get; }

    /// <summary> Projects a persisted operation detail when every persisted value is canonical and valid. </summary>
    public static bool TryCreate (
        IndexOpsDescribeJsonContract? contract,
        [NotNullWhen(true)]
        out OpsDescribeSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.SourceInputsHash, out var sourceInputsHash)
            || !IndexCatalogContractValidator.TryProjectOpsEntry(
                contract.Operation,
                index: 0,
                allowEditLoweringOnlyEntries: false,
                requireCanonicalLiterals: true,
                out var operation,
                out _))
        {
            return false;
        }

        snapshot = new OpsDescribeSnapshot(
            contract.GeneratedAtUtc,
            sourceInputsHash,
            operation);
        return true;
    }
}
