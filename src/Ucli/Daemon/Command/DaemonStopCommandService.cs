using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Implements daemon-stop command workflow orchestration. </summary>
internal sealed class DaemonStopCommandService : IDaemonStopCommandService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStopOperation daemonStopOperation;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopCommandService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStopOperation"> The daemon stop-operation dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopCommandService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonStopOperation daemonStopOperation)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
    }

    /// <summary> Executes one daemon-stop workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-stop execution result. </returns>
    public async ValueTask<DaemonStopExecutionResult> Stop (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonStop,
                projectPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var stopResult = await daemonStopOperation.Stop(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(stopResult.Error ?? ExecutionError.InternalError(
                "Daemon stop operation failed without structured error details."));
        }

        if (!DaemonStopStateCodec.TryToValue(stopResult.Status, out var stopStatus))
        {
            return DaemonStopExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon stop returned unsupported status: {stopResult.Status}."));
        }

        var output = new DaemonStopExecutionOutput(
            StopStatus: stopStatus!,
            DaemonStatus: DaemonStatusStateCodec.NotRunning,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: null);
        return DaemonStopExecutionResult.Success(output);
    }
}