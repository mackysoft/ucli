using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;

/// <summary> Implements polling orchestration for <c>logs daemon read</c> command execution. </summary>
internal sealed class LogsDaemonService : ILogsDaemonService
{
    private readonly LogsStreamPollingExecutor streamPollingExecutor;

    private readonly IDaemonLogsClient daemonLogsClient;

    private readonly ILogsDaemonRequestValidator requestValidator;

    /// <summary> Initializes a new instance of the <see cref="LogsDaemonService" /> class. </summary>
    /// <param name="streamPollingExecutor"> The shared log-stream polling executor dependency. </param>
    /// <param name="daemonLogsClient"> The daemon-log IPC client dependency. </param>
    /// <param name="requestValidator"> The command request validator dependency. </param>
    public LogsDaemonService (
        LogsStreamPollingExecutor streamPollingExecutor,
        IDaemonLogsClient daemonLogsClient,
        ILogsDaemonRequestValidator requestValidator)
    {
        this.streamPollingExecutor = streamPollingExecutor ?? throw new ArgumentNullException(nameof(streamPollingExecutor));
        this.daemonLogsClient = daemonLogsClient ?? throw new ArgumentNullException(nameof(daemonLogsClient));
        this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
    }

    /// <inheritdoc />
    public ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!requestValidator.TryValidate(request, out var query, out var streamOptions, out var argumentValidationError))
        {
            return ValueTask.FromResult(LogsReadServiceResult.Failure(argumentValidationError!, 0, null));
        }

        return streamPollingExecutor.ExecuteAsync(
            UcliCommandIds.LogsDaemonRead,
            request.ProjectPath,
            request.TimeoutMilliseconds,
            query!,
            request.Stream,
            streamOptions!,
            daemonLogsClient.ReadAsync,
            static readResult => readResult.Response,
            static readResult => readResult.Error,
            static (query, after) => new IpcDaemonLogsReadRequest(
                Tail: null,
                After: after,
                Since: query.Since,
                Until: query.Until,
                Level: query.Level,
                Query: query.Query,
                QueryTarget: query.QueryTarget,
                Category: query.Category),
            static response => response.Events,
            static response => response.NextCursor.Value,
            static daemonLogEvent => daemonLogEvent.Cursor.Value,
            onEvent,
            static daemonLogEvent => daemonLogEvent.Timestamp,
            cancellationToken);
    }
}
