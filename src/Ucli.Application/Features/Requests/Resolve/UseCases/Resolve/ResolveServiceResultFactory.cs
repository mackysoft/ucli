using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates normalized resolve service results. </summary>
internal static class ResolveServiceResultFactory
{
    /// <summary> Creates one successful resolve result. </summary>
    public static ResolveServiceResult Success (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        ReadIndexInfo readIndex)
    {
        return Create(requestId, opResults, [], ApplicationOutcome.Success, readIndex);
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
                ? ApplicationOutcome.InvalidArgument
                : ApplicationOutcome.ToolError,
            readIndex ?? CreateUnityReadIndexInfo(fallbackReason: null));
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
            ExecuteResponseConverter.ResolveOutcome(error.Code),
            readIndex);
    }

    /// <summary> Creates one normalized resolve service result. </summary>
    public static ResolveServiceResult Create (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        ApplicationOutcome outcome,
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
            Outcome: outcome,
            ReadIndex: readIndex);
    }

    private static ReadIndexInfo CreateUnityReadIndexInfo (string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoTextCodec.SourceUnity,
            Freshness: ReadIndexInfoTextCodec.FreshnessFresh,
            GeneratedAtUtc: null,
            FallbackReason: fallbackReason);
    }
}
