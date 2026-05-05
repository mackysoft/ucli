using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;

/// <summary> Implements polling orchestration for <c>logs daemon</c> command execution. </summary>
internal sealed class LogsDaemonService : ILogsDaemonService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonLogsClient daemonLogsClient;

    private readonly ILogsDaemonRequestValidator requestValidator;

    private readonly IDaemonLogsStreamTerminationPolicy streamTerminationPolicy;

    /// <summary> Initializes a new instance of the <see cref="LogsDaemonService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command context resolver dependency. </param>
    /// <param name="daemonLogsClient"> The daemon-log IPC client dependency. </param>
    /// <param name="requestValidator"> The command request validator dependency. </param>
    /// <param name="streamTerminationPolicy"> The stream-termination policy dependency. </param>
    public LogsDaemonService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonLogsClient daemonLogsClient,
        ILogsDaemonRequestValidator requestValidator,
        IDaemonLogsStreamTerminationPolicy streamTerminationPolicy)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonLogsClient = daemonLogsClient ?? throw new ArgumentNullException(nameof(daemonLogsClient));
        this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
        this.streamTerminationPolicy = streamTerminationPolicy ?? throw new ArgumentNullException(nameof(streamTerminationPolicy));
    }

    /// <inheritdoc />
    public ValueTask<LogsDaemonServiceResult> Execute (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!requestValidator.TryValidate(request, out var query, out var streamOptions, out var argumentValidationError))
        {
            return ValueTask.FromResult(LogsDaemonServiceResult.Failure(argumentValidationError!));
        }

        return LogsStreamPollingExecutor.Execute(
            daemonCommandExecutionContextResolver,
            UcliCommandIds.LogsDaemon,
            request.ProjectPath,
            query!,
            request.Stream,
            streamOptions!,
            daemonLogsClient.Read,
            static readResult => readResult.Response,
            static readResult => readResult.Error,
            static (query, after) => query with
            {
                Tail = null,
                After = after,
            },
            static response => response.Events,
            static response => response.NextCursor,
            onEvent,
            streamTerminationPolicy,
            static daemonLogEvent => daemonLogEvent.Timestamp,
            cancellationToken);
    }
}
