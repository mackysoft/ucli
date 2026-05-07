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
        UcliErrorCode? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = RequestServiceResultPolicy.FromExecutionError(error, errorCode);
        return PlanServiceResult.Failure(
            error.Message,
            [
                executionError,
            ],
            RequestServiceResultPolicy.ResolveOutcome(error, errorCode),
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
        return PlanServiceResult.Failure(
            "Static validation failed.",
            RequestServiceResultPolicy.FromValidationErrors(validationErrors),
            ApplicationOutcome.InvalidArgument,
            output);
    }
}
