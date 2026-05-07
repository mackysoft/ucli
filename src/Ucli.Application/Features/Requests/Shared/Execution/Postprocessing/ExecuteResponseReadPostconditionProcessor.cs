using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Postprocessing;

/// <summary> Applies mutation read-postcondition persistence policy to converted execute responses. </summary>
internal static class ExecuteResponseReadPostconditionProcessor
{
    public static async ValueTask<(ExecuteResponseConversionResult Response, OperationExecutionError? PersistenceError)> Persist (
        ExecuteResponseConversionResult response,
        IMutationReadPostconditionStore store,
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();

        var persistenceFailure = response.ReadPostcondition == null || response.ReadPostcondition.Requirements.Count == 0
            ? null
            : (await store.WriteMerged(
                    storageRoot,
                    projectFingerprint,
                    response.ReadPostcondition,
                    cancellationToken)
                .ConfigureAwait(false)).Error;
        if (persistenceFailure == null)
        {
            return (response, null);
        }

        var persistenceError = new OperationExecutionError(
            UcliCoreErrorCodes.InternalError,
            persistenceFailure.Message,
            null);
        return (
            response with
            {
                Errors = AppendError(response.Errors, persistenceError),
                Outcome = ApplicationOutcome.ToolError,
            },
            persistenceError);
    }

    private static IReadOnlyList<OperationExecutionError> AppendError (
        IReadOnlyList<OperationExecutionError> errors,
        OperationExecutionError persistenceError)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(persistenceError);

        var mergedErrors = new OperationExecutionError[errors.Count + 1];
        for (var i = 0; i < errors.Count; i++)
        {
            mergedErrors[i] = errors[i];
        }

        mergedErrors[^1] = persistenceError;
        return mergedErrors;
    }
}
