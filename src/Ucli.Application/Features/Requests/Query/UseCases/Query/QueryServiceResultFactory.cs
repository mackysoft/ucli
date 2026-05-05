using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
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
        return Create(
            commandName,
            requestId,
            opResults,
            [],
            ApplicationOutcome.Success,
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

        var errorCode = ExecutionErrorCodeMapper.ToCode(error.Kind);
        return Create(
            commandName,
            requestId,
            [],
            [
                new OperationExecutionError(errorCode, error.Message, null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? ApplicationOutcome.InvalidArgument
                : ApplicationOutcome.ToolError,
            error.Message,
            readIndex ?? CreateUnityReadIndexInfo(fallbackReason: null));
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static QueryServiceResult FromIpcError (
        string commandName,
        string requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(
            commandName,
            requestId,
            [],
            [error],
            ExecuteResponseConverter.ResolveOutcome(error.Code),
            string.IsNullOrWhiteSpace(error.Message) ? FailureMessage : error.Message,
            readIndex);
    }

    /// <summary> Creates one normalized typed-query service result. </summary>
    public static QueryServiceResult Create (
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
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(readIndex);

        return new QueryServiceResult(
            CommandName: commandName,
            RequestId: requestId,
            OpResults: opResults,
            Errors: errors,
            Outcome: outcome,
            Message: message,
            ReadIndex: readIndex);
    }

    private static ReadIndexInfo CreateUnityReadIndexInfo (string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoSource.Unity,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: null,
            FallbackReason: fallbackReason);
    }
}
