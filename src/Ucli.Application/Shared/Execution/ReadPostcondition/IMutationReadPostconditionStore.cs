using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Persists fingerprint-scoped mutation read-postcondition state. </summary>
internal interface IMutationReadPostconditionStore
{
    /// <summary> Reads the persisted read-postcondition state when present. </summary>
    ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Merges and writes read-postcondition requirements for one fingerprint. </summary>
    ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcExecuteReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default);
}
