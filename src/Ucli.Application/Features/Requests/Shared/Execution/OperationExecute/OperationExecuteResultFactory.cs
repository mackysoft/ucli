using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Creates normalized operation-execution results across fixed-operation workflows. </summary>
internal static class OperationExecuteResultFactory
{
    private const string DefaultFailureMessage = "Operation execution failed.";

    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return FromExecutionError(Guid.NewGuid().ToString("D"), error);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="error"> The structured execution error. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (
        string requestId,
        ExecutionError error,
        string? failureMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            failureMessage);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromValidationErrors (
        string requestId,
        IReadOnlyList<ValidationError> validationErrors,
        string? failureMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return Failure(
            requestId,
            [],
            RequestFailureNormalizer.FromValidationErrors(validationErrors),
            failureMessage);
    }

    /// <summary> Creates one successful operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="message"> The user-facing success message. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        return OperationExecuteResult.Success(requestId, opResults, message, readPostcondition);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        return Failure(
            requestId,
            opResults,
            errors,
            failureMessage: null,
            readPostcondition);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string? failureMessage,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);

        return OperationExecuteResult.Failure(
            requestId,
            opResults,
            errors,
            RequestFailureNormalizer.ResolveMessage(errors, failureMessage ?? DefaultFailureMessage),
            readPostcondition);
    }
}
