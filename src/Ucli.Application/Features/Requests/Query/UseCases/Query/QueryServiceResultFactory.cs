using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Creates normalized typed-query service results. </summary>
internal static class QueryServiceResultFactory
{
    private const string SuccessMessage = "uCLI query completed.";

    private const string FailureMessage = "uCLI query failed.";

    /// <summary> Creates one successful typed-query result. </summary>
    public static QueryServiceResult Success (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex)
    {
        return QueryServiceResult.Success(
            commandName,
            requestId,
            opResults,
            SuccessMessage,
            readIndex);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static QueryServiceResult FromExecutionError (
        string commandName,
        string requestId,
        ExecutionError error,
        ReadIndexInfo? readIndex = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = RequestServiceResultPolicy.FromExecutionError(error);
        return Failure(
            commandName,
            requestId,
            [],
            [
                executionError,
            ],
            error.Message,
            readIndex ?? ReadIndexInfoFactory.Unity(fallbackReason: null));
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static QueryServiceResult FromIpcError (
        string commandName,
        string requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        var normalizedError = RequestServiceResultPolicy.NormalizeError(error, FailureMessage);
        return Failure(
            commandName,
            requestId,
            [],
            [normalizedError],
            normalizedError.Message,
            readIndex);
    }

    /// <summary> Creates one failed typed-query result. </summary>
    public static QueryServiceResult Failure (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        return QueryServiceResult.Failure(
            commandName,
            requestId,
            opResults,
            errors,
            message,
            readIndex);
    }
}
