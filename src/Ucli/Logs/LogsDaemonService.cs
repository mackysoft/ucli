using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon.Command;

namespace MackySoft.Ucli.Logs;

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
    public async ValueTask<LogsDaemonServiceResult> Execute (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!requestValidator.TryValidate(request, out var validatedRequest, out var argumentValidationError))
        {
            return LogsDaemonServiceResult.Failure(argumentValidationError!);
        }

        var contextResolutionResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.LogsDaemon,
                request.ProjectPath,
                timeout: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return LogsDaemonServiceResult.Failure(contextResolutionResult.Error!);
        }

        var executionContext = contextResolutionResult.Context!;
        string? nextAfterCursor = request.After;
        var lastEventTimestamp = DateTimeOffset.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readQuery = new DaemonLogsReadQuery(
                Tail: request.Tail,
                After: nextAfterCursor,
                Since: request.Since,
                Until: request.Until,
                Level: request.Level,
                Query: request.Query,
                QueryTarget: request.QueryTarget,
                Category: request.Category);
            var readResult = await daemonLogsClient.Read(
                    executionContext.Context.UnityProject,
                    readQuery,
                    executionContext.Timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return LogsDaemonServiceResult.Failure(readResult.Error!);
            }

            var payload = readResult.Response!;
            if (payload.Events.Length > 0)
            {
                lastEventTimestamp = DateTimeOffset.UtcNow;
            }

            foreach (var daemonLogEvent in payload.Events)
            {
                await onEvent(daemonLogEvent, payload.NextCursor, cancellationToken).ConfigureAwait(false);
            }

            if (!request.Stream)
            {
                return LogsDaemonServiceResult.Success();
            }

            nextAfterCursor = payload.NextCursor;
            var now = DateTimeOffset.UtcNow;
            if (streamTerminationPolicy.ShouldStop(
                    payload.Events,
                    now,
                    validatedRequest.UntilTimestamp,
                    lastEventTimestamp,
                    validatedRequest.IdleTimeout))
            {
                return LogsDaemonServiceResult.Success();
            }

            await Task.Delay(validatedRequest.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}