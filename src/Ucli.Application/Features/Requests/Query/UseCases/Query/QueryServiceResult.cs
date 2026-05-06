using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents the normalized result returned from one typed-query execution workflow. </summary>
internal sealed record QueryServiceResult
{
    private QueryServiceResult (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        string message,
        ReadIndexInfo readIndex)
    {
        CommandName = commandName;
        RequestId = requestId;
        OpResults = opResults;
        Errors = errors;
        Outcome = outcome;
        Message = message;
        ReadIndex = readIndex;
    }

    /// <summary> Gets the command name associated with this typed-query result. </summary>
    public string CommandName { get; }

    /// <summary> Gets the request identifier associated with this query execution. </summary>
    public string RequestId { get; }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<OperationExecutionError> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the read-index metadata associated with this result. </summary>
    public ReadIndexInfo ReadIndex { get; }

    /// <summary> Gets a value indicating whether query execution succeeded. </summary>
    public bool IsSuccess => Outcome == ApplicationOutcome.Success;

    /// <summary> Creates one successful typed-query result. </summary>
    internal static QueryServiceResult Success (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        ReadIndexInfo readIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        RequestServiceResultPolicy.ValidateSuccessMessage(message);

        return new QueryServiceResult(
            commandName,
            requestId,
            opResults,
            RequestServiceResultPolicy.EmptyErrors,
            ApplicationOutcome.Success,
            message,
            readIndex);
    }

    /// <summary> Creates one failed typed-query result. </summary>
    internal static QueryServiceResult Failure (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        string message,
        ReadIndexInfo readIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        RequestServiceResultPolicy.ValidateFailureMessage(message);

        return new QueryServiceResult(
            commandName,
            requestId,
            opResults,
            RequestServiceResultPolicy.RequireFailureErrors(errors, outcome),
            outcome,
            message,
            readIndex);
    }
}
