using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Creates normalized operation-execution results across fixed-operation workflows. </summary>
internal static class OperationExecuteResultFactory
{
    private const string DefaultFailureMessage = "Operation execution failed.";

    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="error"> The structured execution error. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (
        Guid requestId,
        ExecutionError error,
        string? failureMessage = null,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            failureMessage,
            project: project);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromValidationErrors (
        Guid requestId,
        IReadOnlyList<ValidationError> validationErrors,
        string? failureMessage = null,
        ProjectIdentityInfo? project = null)
    {

        return Failure(
            requestId,
            [],
            RequestFailureNormalizer.FromValidationErrors(validationErrors),
            failureMessage,
            project: project);
    }

    /// <summary> Creates one successful operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="message"> The user-facing success message. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <param name="postReadSource"> The source facts used by post-read verification. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Success (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        OperationExecutionReadPostcondition? readPostcondition,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        return OperationExecuteResult.Success(requestId, opResults, message, readPostcondition, project, contractViolations, postReadSource);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="failureMessage"> The fallback user-facing failure message. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <param name="postReadSource"> The source facts used by post-read verification. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Failure (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string? failureMessage = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null,
        OperationExecutionReadPostcondition? readPostcondition = null,
        ProjectIdentityInfo? project = null,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);

        return OperationExecuteResult.Failure(
            requestId,
            opResults,
            errors,
            RequestFailureNormalizer.ResolveMessage(errors, failureMessage ?? DefaultFailureMessage),
            contractViolations,
            readPostcondition,
            project,
            postReadSource);
    }
}
