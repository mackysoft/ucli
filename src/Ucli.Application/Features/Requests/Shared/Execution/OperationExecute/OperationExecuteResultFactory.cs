using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Creates normalized operation-execution results across fixed-operation workflows. </summary>
internal static class OperationExecuteResultFactory
{
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
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (
        string requestId,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(error);

        var executionError = RequestServiceResultPolicy.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            RequestServiceResultPolicy.ResolveOutcome(error));
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromValidationErrors (
        string requestId,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return Failure(
            requestId,
            [],
            RequestServiceResultPolicy.FromValidationErrors(validationErrors),
            ApplicationOutcome.InvalidArgument);
    }

    /// <summary> Creates one successful operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        return OperationExecuteResult.Success(requestId, opResults, readPostcondition);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="outcome"> The associated application outcome. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);

        return OperationExecuteResult.Failure(
            requestId,
            opResults,
            errors,
            outcome,
            readPostcondition);
    }
}
