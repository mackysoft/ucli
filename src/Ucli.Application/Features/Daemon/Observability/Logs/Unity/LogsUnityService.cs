using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Implements polling orchestration for <c>logs unity read</c> command execution. </summary>
internal sealed class LogsUnityService : ILogsUnityService
{
    private readonly LogsStreamPollingExecutor streamPollingExecutor;

    private readonly IUnityLogsClient unityLogsClient;

    private readonly ILogsUnityRequestValidator requestValidator;

    /// <summary> Initializes a new instance of the <see cref="LogsUnityService" /> class. </summary>
    public LogsUnityService (
        LogsStreamPollingExecutor streamPollingExecutor,
        IUnityLogsClient unityLogsClient,
        ILogsUnityRequestValidator requestValidator)
    {
        this.streamPollingExecutor = streamPollingExecutor ?? throw new ArgumentNullException(nameof(streamPollingExecutor));
        this.unityLogsClient = unityLogsClient ?? throw new ArgumentNullException(nameof(unityLogsClient));
        this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
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
            return ValueTask.FromResult(LogsReadServiceResult.Failure(argumentValidationError!, 0, null));
        }

        return streamPollingExecutor.ExecuteAsync(
            UcliCommandIds.LogsUnityRead,
            request.ProjectPath,
            request.TimeoutMilliseconds,
            query!,
            request.Stream,
            streamOptions!,
            unityLogsClient.ReadAsync,
            static readResult => readResult.Response,
            static readResult => readResult.Error,
            static (query, after) => new IpcUnityLogsReadRequest(
                Tail: null,
                After: after,
                Since: query.Since,
                Until: query.Until,
                Level: query.Level,
                Query: query.Query,
                QueryTarget: query.QueryTarget,
                Source: query.Source,
                StackTrace: query.StackTrace,
                StackTraceMaxFrames: query.StackTraceMaxFrames,
                StackTraceMaxChars: query.StackTraceMaxChars),
            static response => response.Events,
            static response => response.NextCursor.Value,
            static unityLogEvent => unityLogEvent.Cursor.Value,
            onEvent,
            static unityLogEvent => unityLogEvent.Timestamp,
            cancellationToken);
    }
}
