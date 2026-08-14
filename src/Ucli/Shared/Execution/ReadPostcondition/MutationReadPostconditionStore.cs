using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;

namespace MackySoft.Ucli.Shared.Execution.ReadPostcondition;

/// <summary> Persists fingerprint-scoped mutation read-postcondition state under <c>.ucli/local</c>. </summary>
internal sealed class MutationReadPostconditionStore : IMutationReadPostconditionStore
{
    private readonly MutationReadPostconditionJournal journal = new();

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        var result = await journal.ReadOrNullAsync(storageRoot, projectFingerprint, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? MutationReadPostconditionReadResult.Success(result.ReadPostcondition)
            : MutationReadPostconditionReadResult.Failure(ToExecutionError(result.Failure!));
    }

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        ExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        var result = await journal.WriteMergedAsync(storageRoot, projectFingerprint, readPostcondition, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? MutationReadPostconditionStoreOperationResult.Success()
            : MutationReadPostconditionStoreOperationResult.Failure(ToExecutionError(result.Failure!));
    }

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionStoreOperationResult> InvalidateAfterUnobservedEvalCallAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        var result = await journal.InvalidateAfterUnobservedEvalCallAsync(storageRoot, projectFingerprint, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? MutationReadPostconditionStoreOperationResult.Success()
            : MutationReadPostconditionStoreOperationResult.Failure(ToExecutionError(result.Failure!));
    }

    private static ExecutionError ToExecutionError (MutationReadPostconditionJournalFailure failure)
    {
        return failure.Kind == MutationReadPostconditionJournalFailureKind.InvalidDocument
            ? ExecutionError.InvalidArgument(failure.Message)
            : ExecutionError.InternalError(failure.Message);
    }
}
