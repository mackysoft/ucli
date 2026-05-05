using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;

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
        TimeProvider? timeProvider = null)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonProjectLifecycleGateway = daemonProjectLifecycleGateway ?? throw new ArgumentNullException(nameof(daemonProjectLifecycleGateway));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        var deadline = ExecutionDeadline.Start(executionContext.Timeout, timeProvider);
        DaemonStopResult? stopResult = null;
        if (deadline.TryGetRemainingTimeout(out var projectLifecycleTimeout))
        {
            stopResult = await daemonProjectLifecycleGateway.TryStopProject(
                    executionContext.Context.UnityProject,
                    projectLifecycleTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (stopResult == null)
        {
            if (!deadline.TryGetRemainingTimeout(out var directStopTimeout))
            {
                return DaemonStopExecutionResult.Failure(ExecutionError.Timeout(
                    "Timed out before daemon stop fallback could begin."));
            }

            stopResult = await daemonStopOperation.Stop(
                    executionContext.Context.UnityProject,
                    directStopTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }

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
