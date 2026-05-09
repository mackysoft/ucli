using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Persists fingerprint-scoped mutation read-postcondition state. </summary>
internal interface IMutationReadPostconditionStore
{
    /// <summary> Reads the persisted read-postcondition state when present. </summary>
    ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Merges and writes read-postcondition requirements for one fingerprint. </summary>
    ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        string storageRoot,
        string projectFingerprint,
        OperationExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default);
}
