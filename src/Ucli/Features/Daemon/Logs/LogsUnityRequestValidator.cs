using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Logs;

/// <summary> Validates raw <c>logs unity</c> request values. </summary>
internal sealed class LogsUnityRequestValidator : ILogsUnityRequestValidator
{
    /// <inheritdoc />
    public bool TryValidate (
        LogsUnityServiceRequest request,
        out IpcUnityLogsReadRequest? query,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        query = null;
        streamOptions = null;

        var ipcRequest = new IpcUnityLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: request.Level,
            Query: request.Query,
            QueryTarget: request.QueryTarget,
            Source: request.Source,
            StackTrace: request.StackTrace,
            StackTraceMaxFrames: request.StackTraceMaxFrames,
            StackTraceMaxChars: request.StackTraceMaxChars);
        if (!IpcUnityLogsReadRequestNormalizer.TryNormalize(
                ipcRequest,
                out var normalizedQuery,
                out _,
                out var untilTimestamp,
                out var commonValidationErrorMessage))
        {
            error = ExecutionError.InvalidArgument(commonValidationErrorMessage!);
            return false;
        }

        if (!LogsStreamRuntimeOptionsValidator.TryValidate(
                request.Stream,
                request.PollIntervalMilliseconds,
                request.IdleTimeoutMilliseconds,
                untilTimestamp,
                out var validatedStreamOptions,
                out error))
        {
            return false;
        }

        query = normalizedQuery;
        streamOptions = validatedStreamOptions;
        return true;
    }
}