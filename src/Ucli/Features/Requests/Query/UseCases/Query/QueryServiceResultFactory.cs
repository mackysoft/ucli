using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Creates normalized typed-query service results. </summary>
internal static class QueryServiceResultFactory
{
    private const string SuccessMessage = "uCLI query completed.";

    private const string FailureMessage = "uCLI query failed.";

    /// <summary> Creates one successful typed-query result. </summary>
    public static QueryServiceResult Success (
        string commandName,
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        ReadIndexInfo readIndex)
    {
        return Create(
            commandName,
            requestId,
            opResults,
            [],
            (int)CliExitCode.Success,
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
                new IpcError(errorCode, error.Message, null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? (int)CliExitCode.InvalidArgument
                : (int)CliExitCode.ToolError,
            error.Message,
            readIndex ?? QueryReadIndexInfoFactory.Unity(fallbackReason: null));
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static QueryServiceResult FromIpcError (
        string commandName,
        string requestId,
        IpcError error,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(
            commandName,
            requestId,
            [],
            [error],
            ExecuteResponseConverter.ResolveExitCode(error.Code),
            string.IsNullOrWhiteSpace(error.Message) ? FailureMessage : error.Message,
            readIndex);
    }

    /// <summary> Creates one normalized typed-query service result. </summary>
    public static QueryServiceResult Create (
        string commandName,
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        int exitCode,
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
            ProtocolVersion: IpcProtocol.CurrentVersion,
            CommandName: commandName,
            RequestId: requestId,
            OpResults: opResults,
            Errors: errors,
            ExitCode: exitCode,
            Message: message,
            ReadIndex: readIndex);
    }
}
