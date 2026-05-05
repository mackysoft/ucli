using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Provides shared persistence helpers for mutation read-postcondition write sites. </summary>
internal static class MutationReadPostconditionPersistence
{
    public static async ValueTask<ExecutionError?> Write (
        IMutationReadPostconditionStore store,
        string storageRoot,
        string projectFingerprint,
        IpcExecuteReadPostcondition? readPostcondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();

        if (readPostcondition == null || readPostcondition.IsEmpty)
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
