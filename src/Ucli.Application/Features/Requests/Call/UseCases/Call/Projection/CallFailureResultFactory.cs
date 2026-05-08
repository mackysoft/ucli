using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;

/// <summary> Creates normalized failure results for the <c>call</c> feature. </summary>
internal static class CallFailureResultFactory
{
    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="error"> The execution error. </param>
    /// <param name="output"> The available call payload. </param>
    /// <param name="errorCode"> The optional machine-readable error code. </param>
    /// <returns> The normalized failure result. </returns>
    public static CallServiceResult FromExecutionError (
        ExecutionError error,
        CallExecutionOutput? output = null,
        UcliErrorCode? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = RequestServiceResultPolicy.FromExecutionError(error, errorCode);
        return CallServiceResult.Failure(
            error.Message,
            [
                executionError,
            ],
            output);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="validationErrors"> The validation errors. </param>
    /// <param name="output"> The available call payload. </param>
    /// <returns> The normalized failure result. </returns>
    public static CallServiceResult FromValidationErrors (
        IReadOnlyList<ValidationError> validationErrors,
        CallExecutionOutput? output = null)
    {
        return CallServiceResult.Failure(
            "Static validation failed.",
            RequestServiceResultPolicy.FromValidationErrors(validationErrors),
            output);
    }
}
