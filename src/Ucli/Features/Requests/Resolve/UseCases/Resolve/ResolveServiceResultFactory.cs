using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates normalized resolve service results. </summary>
internal static class ResolveServiceResultFactory
{
    /// <summary> Creates one successful resolve result. </summary>
    public static ResolveServiceResult Success (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        ReadIndexInfo readIndex)
    {
        return Create(requestId, opResults, [], (int)CliExitCode.Success, readIndex);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static ResolveServiceResult FromExecutionError (
        string requestId,
        ExecutionError error,
        ReadIndexInfo? readIndex = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var errorCode = ExecutionErrorCodeMapper.ToCode(error.Kind);
        return Create(
            requestId,
            [],
            [
                new IpcError(errorCode, error.Message, null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? (int)CliExitCode.InvalidArgument
                : (int)CliExitCode.ToolError,
            readIndex ?? ResolveReadIndexInfoFactory.Unity(fallbackReason: null));
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static ResolveServiceResult FromIpcError (
        string requestId,
        IpcError error,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(
            requestId,
            [],
            [error],
            ExecuteResponseConverter.ResolveExitCode(error.Code),
            readIndex);
    }

    /// <summary> Creates one normalized resolve service result. </summary>
    public static ResolveServiceResult Create (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        int exitCode,
        ReadIndexInfo readIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(readIndex);

        return new ResolveServiceResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            OpResults: opResults,
            Errors: errors,
            ExitCode: exitCode,
            ReadIndex: readIndex);
    }
}
