using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;

/// <summary> Implements daemon-stop command workflow orchestration. </summary>
internal sealed class DaemonStopService : IDaemonStopService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonProjectLifecycleGateway daemonProjectLifecycleGateway;

    private readonly IDaemonStopOperation daemonStopOperation;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonProjectLifecycleGateway"> The daemon project-lifecycle gateway dependency. </param>
    /// <param name="daemonStopOperation"> The daemon stop-operation dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonProjectLifecycleGateway daemonProjectLifecycleGateway,
        IDaemonStopOperation daemonStopOperation,
        TimeProvider timeProvider)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonProjectLifecycleGateway = daemonProjectLifecycleGateway ?? throw new ArgumentNullException(nameof(daemonProjectLifecycleGateway));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Executes one daemon-stop workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-stop execution result. </returns>
    public async ValueTask<DaemonStopExecutionResult> StopAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                UcliCommandIds.DaemonStop,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var deadline = ExecutionDeadline.Start(executionContext.Timeout, timeProvider);
        DaemonStopResult? stopResult = null;
        DaemonStopResult? supervisorStopFailure = null;
        if (deadline.TryGetRemainingTimeout(out var projectLifecycleTimeout))
        {
            stopResult = await daemonProjectLifecycleGateway.TryStopProjectAsync(
                    executionContext.Context.UnityProject,
                    ResolveSupervisorStopTimeout(projectLifecycleTimeout),
                    cancellationToken)
                .ConfigureAwait(false);
            if (ShouldFallbackToDirectStop(stopResult))
            {
                supervisorStopFailure = stopResult;
                stopResult = null;
            }
        }

        if (stopResult == null)
        {
            if (!deadline.TryGetRemainingTimeout(out _))
            {
                if (supervisorStopFailure?.Error is not null)
                {
                    return DaemonStopExecutionResult.Failure(supervisorStopFailure.Error);
                }

                return DaemonStopExecutionResult.Failure(ExecutionError.Timeout(
                    "Timed out before daemon stop fallback could begin."));
            }

            stopResult = await daemonStopOperation.StopAsync(
                    executionContext.Context.UnityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!stopResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(stopResult.Error);
        }

        var output = new DaemonStopExecutionOutput(
            StopStatus: stopResult.Status.Value,
            DaemonStatus: DaemonStatusKind.NotRunning,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: null);
        return DaemonStopExecutionResult.Success(output);
    }

    private static TimeSpan ResolveSupervisorStopTimeout (TimeSpan remainingTimeout)
    {
        // NOTE:
        // The supervisor is a convenience control plane. Stop must keep enough budget for the direct daemon
        // termination path, otherwise a Play Mode-stalled supervisor can prevent the owned process cleanup.
        return remainingTimeout > DaemonTimeouts.StopCompensationTimeout
            ? remainingTimeout - DaemonTimeouts.StopCompensationTimeout
            : remainingTimeout;
    }

    private static bool ShouldFallbackToDirectStop (DaemonStopResult? stopResult)
    {
        return stopResult is { IsSuccess: false, Error.Kind: ExecutionErrorKind.Timeout };
    }

}
