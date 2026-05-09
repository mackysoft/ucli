using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Implements <c>logs unity clear</c> command orchestration. </summary>
internal sealed class LogsUnityClearService : ILogsUnityClearService
{
    private const string ClearedStatus = "cleared";

    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IUnityConsoleClearClient unityConsoleClearClient;

    /// <summary> Initializes a new instance of the <see cref="LogsUnityClearService" /> class. </summary>
    public LogsUnityClearService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IUnityConsoleClearClient unityConsoleClearClient)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.unityConsoleClearClient = unityConsoleClearClient ?? throw new ArgumentNullException(nameof(unityConsoleClearClient));
    }

    /// <inheritdoc />
    public async ValueTask<LogsUnityClearServiceResult> ExecuteAsync (
        LogsUnityClearServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var contextResolutionResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                UcliCommandIds.LogsUnityClear,
                request.ProjectPath,
                request.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return LogsUnityClearServiceResult.Failure(contextResolutionResult.Error!);
        }

        var context = contextResolutionResult.Context!;
        var clearResult = await unityConsoleClearClient.ClearAsync(
                context.Context.UnityProject,
                context.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!clearResult.IsSuccess)
        {
            return LogsUnityClearServiceResult.Failure(clearResult.Error!);
        }

        return LogsUnityClearServiceResult.Success(new LogsUnityClearServiceOutput(
            ClearStatus: ClearedStatus,
            TimeoutMilliseconds: (int)context.Timeout.TotalMilliseconds));
    }
}
