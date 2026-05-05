using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Provides shared persistence helpers for mutation read-postcondition write sites. </summary>
internal static class MutationReadPostconditionPersistence
{
    public static async ValueTask<ExecutionError?> Write (
        IMutationReadPostconditionStore store,
        string storageRoot,
        string projectFingerprint,
        OperationExecutionReadPostcondition? readPostcondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();

        if (readPostcondition == null || readPostcondition.Requirements.Count == 0)
        {
            return null;
        }

        return (await store.WriteMerged(
                storageRoot,
                projectFingerprint,
                readPostcondition,
                cancellationToken)
            .ConfigureAwait(false)).Error;
    }
}
