using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Shared.Execution.ReadPostcondition;

/// <summary> Provides shared persistence helpers for mutation read-postcondition write sites. </summary>
internal static class MutationReadPostconditionPersistence
{
    public static async ValueTask<IpcError?> WriteOrCreateError (
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

        var writeResult = await store.WriteMerged(
                storageRoot,
                projectFingerprint,
                readPostcondition,
                cancellationToken)
            .ConfigureAwait(false);
        if (writeResult.IsSuccess)
        {
            return null;
        }

        return new IpcError(
            IpcErrorCodes.InternalError,
            writeResult.Error!.Message,
            null);
    }
}