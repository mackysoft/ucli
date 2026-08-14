using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Persists fingerprint-scoped mutation read-postcondition state. </summary>
internal interface IMutationReadPostconditionStore
{
    /// <summary> Reads the persisted read-postcondition state when present. </summary>
    ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Merges and writes read-postcondition requirements for one fingerprint. </summary>
    ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        ExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default);

    /// <summary> Persists broad read fences after an eval call was sent but its response was not recovered. </summary>
    ValueTask<MutationReadPostconditionStoreOperationResult> InvalidateAfterUnobservedEvalCallAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);
}
