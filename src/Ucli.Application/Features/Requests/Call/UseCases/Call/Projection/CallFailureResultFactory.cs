using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

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
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return CallServiceResult.Failure(
            error.Message,
            [
                new IpcError(
                    string.IsNullOrWhiteSpace(errorCode)
                        ? ExecutionErrorCodeMapper.ToCode(error.Kind)
                        : errorCode,
                    error.Message,
                    null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? ApplicationOutcome.InvalidArgument
                : ApplicationOutcome.ToolError,
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
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new IpcError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new IpcError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return CallServiceResult.Failure(
            "Static validation failed.",
            errors,
            ApplicationOutcome.InvalidArgument,
            output);
    }
}
