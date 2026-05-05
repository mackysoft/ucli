using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;

/// <summary> Applies mutation read-postcondition persistence policy to converted execute responses. </summary>
internal static class ExecuteResponseReadPostconditionProcessor
{
    public static async ValueTask<(ExecuteResponseConversionResult Response, IpcError? PersistenceError)> Persist (
        ExecuteResponseConversionResult response,
        IMutationReadPostconditionStore store,
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();

        var persistenceFailure = await MutationReadPostconditionPersistence.Write(
                store,
                storageRoot,
                projectFingerprint,
                response.ReadPostcondition,
                cancellationToken)
            .ConfigureAwait(false);
        if (persistenceFailure == null)
        {
            return (response, null);
        }

        var persistenceError = new IpcError(
            IpcErrorCodes.InternalError,
            persistenceFailure.Message,
            null);
        return (
            response with
            {
                Errors = AppendError(response.Errors, persistenceError),
                ExitCode = (int)ApplicationExitCode.ToolError,
            },
            persistenceError);
    }

    private static IReadOnlyList<IpcError> AppendError (
        IReadOnlyList<IpcError> errors,
        IpcError persistenceError)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(persistenceError);

        var mergedErrors = new IpcError[errors.Count + 1];
        for (var i = 0; i < errors.Count; i++)
        {
            mergedErrors[i] = errors[i];
        }

        mergedErrors[^1] = persistenceError;
        return mergedErrors;
    }
}
