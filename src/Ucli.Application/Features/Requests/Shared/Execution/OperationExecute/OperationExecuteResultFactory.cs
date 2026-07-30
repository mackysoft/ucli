using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Creates normalized operation-execution results across fixed-operation workflows. </summary>
internal static class OperationExecuteResultFactory
{
    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (
        Guid requestId,
        ExecutionError error,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            contractViolations: [],
            readPostcondition: null,
            project: project,
            postReadSource: null);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromValidationErrors (
        Guid requestId,
        IReadOnlyList<ValidationError> validationErrors,
        ProjectIdentityInfo? project)
    {

        return Failure(
            requestId,
            [],
            RequestFailureNormalizer.FromValidationErrors(validationErrors),
            contractViolations: [],
            readPostcondition: null,
            project,
            postReadSource: null);
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
        IpcExecuteReadPostcondition? readPostcondition,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations,
        OperationExecutionPostReadSource? postReadSource)
    {
        return OperationExecuteResult.Success(requestId, opResults, message, readPostcondition, project, contractViolations, postReadSource);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="readPostcondition"> The emitted mutation read-postcondition payload. </param>
    /// <param name="postReadSource"> The source facts used by post-read verification. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Failure (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations,
        IpcExecuteReadPostcondition? readPostcondition,
        ProjectIdentityInfo? project,
        OperationExecutionPostReadSource? postReadSource)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(contractViolations);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return OperationExecuteResult.Failure(
            requestId,
            opResults,
            failureErrors,
            failureErrors[0].Message,
            contractViolations,
            readPostcondition,
            project,
            postReadSource);
    }
}
