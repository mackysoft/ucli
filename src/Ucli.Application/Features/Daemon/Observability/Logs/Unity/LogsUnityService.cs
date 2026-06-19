using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Implements polling orchestration for <c>logs unity read</c> command execution. </summary>
internal sealed class LogsUnityService : ILogsUnityService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IUnityLogsClient unityLogsClient;

    private readonly ILogsUnityRequestValidator requestValidator;

    private readonly IDaemonLogsStreamTerminationPolicy streamTerminationPolicy;

    /// <summary> Initializes a new instance of the <see cref="LogsUnityService" /> class. </summary>
    public LogsUnityService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IUnityLogsClient unityLogsClient,
        ILogsUnityRequestValidator requestValidator,
        IDaemonLogsStreamTerminationPolicy streamTerminationPolicy)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.unityLogsClient = unityLogsClient ?? throw new ArgumentNullException(nameof(unityLogsClient));
        this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
        this.streamTerminationPolicy = streamTerminationPolicy ?? throw new ArgumentNullException(nameof(streamTerminationPolicy));
    }

    /// <inheritdoc />
    public ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsUnityServiceRequest request,
        Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!requestValidator.TryValidate(request, out var query, out var streamOptions, out var argumentValidationError))
        {
            return ValueTask.FromResult(LogsReadServiceResult.Failure(argumentValidationError!));
        }

        return LogsStreamPollingExecutor.ExecuteAsync(
            daemonCommandExecutionContextResolver,
            UcliCommandIds.LogsUnityRead,
            request.ProjectPath,
            request.TimeoutMilliseconds,
            query!,
            request.Stream,
            streamOptions!,
            unityLogsClient.ReadAsync,
            static readResult => readResult.Response,
            static readResult => readResult.Error,
            static (query, after) => query with
            {
                Tail = null,
                After = after,
            },
            static response => response.Events,
            static response => response.NextCursor,
            static unityLogEvent => unityLogEvent.Cursor,
            onEvent,
            streamTerminationPolicy,
            static unityLogEvent => unityLogEvent.Timestamp,
            cancellationToken);
    }
}
