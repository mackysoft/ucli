using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Validates raw <c>logs unity read</c> request values. </summary>
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

        if (!LogsRequestContractLiteralParser.TryParseLevel(request.Level, out var level, out var literalError)
            || !LogsRequestContractLiteralParser.TryParseQueryTarget(request.QueryTarget, out var queryTarget, out literalError)
            || !LogsRequestContractLiteralParser.TryParseSource(request.Source, out var source, out literalError)
            || !LogsRequestContractLiteralParser.TryParseStackTraceMode(request.StackTrace, out var stackTraceMode, out literalError))
        {
            error = ExecutionError.InvalidArgument(literalError!);
            return false;
        }

        var ipcRequest = new IpcUnityLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: level,
            Query: request.Query,
            QueryTarget: queryTarget,
            Source: source,
            StackTrace: stackTraceMode,
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
