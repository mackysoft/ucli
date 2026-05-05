using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;

/// <summary> Creates normalized failure results for the <c>plan</c> feature. </summary>
internal static class PlanFailureResultFactory
{
    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="error"> The execution error. </param>
    /// <param name="output"> The available plan payload. </param>
    /// <param name="errorCode"> The optional machine-readable error code. </param>
    /// <returns> The normalized failure result. </returns>
    public static PlanServiceResult FromExecutionError (
        ExecutionError error,
        PlanExecutionOutput? output = null,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return PlanServiceResult.Failure(
            error.Message,
            [
                new OperationExecutionError(
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
    /// <param name="output"> The available plan payload. </param>
    /// <returns> The normalized failure result. </returns>
    public static PlanServiceResult FromValidationErrors (
        IReadOnlyList<ValidationError> validationErrors,
        PlanExecutionOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new OperationExecutionError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new OperationExecutionError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return PlanServiceResult.Failure(
            "Static validation failed.",
            errors,
            ApplicationOutcome.InvalidArgument,
            output);
    }
}
