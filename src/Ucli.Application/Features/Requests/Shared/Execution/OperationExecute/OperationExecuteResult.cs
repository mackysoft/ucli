using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents the normalized result returned from one fixed operation execution workflow. </summary>
internal sealed record OperationExecuteResult
{
    private OperationExecuteResult (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        string message,
        OperationExecutionReadPostcondition? readPostcondition)
    {
        RequestId = requestId;
        OpResults = opResults;
        Errors = errors;
        Outcome = outcome;
        Message = message;
        ReadPostcondition = readPostcondition;
    }

    /// <summary> Gets the request identifier associated with this execution. </summary>
    public string RequestId { get; }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<OperationExecutionError> Errors { get; }

    /// <summary> Gets the application outcome associated with this response. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the read postcondition emitted by mutation execution, when available. </summary>
    public OperationExecutionReadPostcondition? ReadPostcondition { get; }

    /// <summary> Gets a value indicating whether the operation execution succeeded. </summary>
    public bool IsSuccess => Outcome == ApplicationOutcome.Success;

    /// <summary> Creates one successful operation execution result. </summary>
    internal static OperationExecuteResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        RequestServiceResultPolicy.ValidateSuccessMessage(message);

        return new OperationExecuteResult(
            requestId,
            opResults,
            RequestServiceResultPolicy.EmptyErrors,
            ApplicationOutcome.Success,
            message,
            readPostcondition);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    internal static OperationExecuteResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        string message,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        RequestServiceResultPolicy.ValidateFailureMessage(message);

        return new OperationExecuteResult(
            requestId,
            opResults,
            RequestServiceResultPolicy.RequireFailureErrors(errors, outcome),
            outcome,
            message,
            readPostcondition);
    }
}
