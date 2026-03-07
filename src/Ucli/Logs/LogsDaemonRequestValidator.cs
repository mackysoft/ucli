using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Logs;

/// <summary> Validates raw <c>logs daemon</c> request values. </summary>
internal sealed class LogsDaemonRequestValidator : ILogsDaemonRequestValidator
{
    /// <inheritdoc />
    public bool TryValidate (
        LogsDaemonServiceRequest request,
        out IpcDaemonLogsReadRequest? query,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        query = null;
        streamOptions = null;

        var ipcRequest = new IpcDaemonLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: request.Level,
            Query: request.Query,
            QueryTarget: request.QueryTarget,
            Category: request.Category);
        if (!IpcDaemonLogsReadRequestNormalizer.TryNormalize(
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