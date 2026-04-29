using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Start;

/// <summary> Implements daemon-start command workflow orchestration. </summary>
internal sealed class DaemonStartService : IDaemonStartService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly ISupervisorProjectGateway supervisorProjectGateway;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="supervisorProjectGateway"> The supervisor project-gateway dependency. </param>
    /// <param name="unityUcliPluginLocator"> The Unity uCLI plugin locator dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        ISupervisorProjectGateway supervisorProjectGateway,
        IUnityUcliPluginLocator unityUcliPluginLocator,
        IDaemonSessionOutputMapper daemonSessionOutputMapper,
        TimeProvider? timeProvider = null)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.supervisorProjectGateway = supervisorProjectGateway ?? throw new ArgumentNullException(nameof(supervisorProjectGateway));
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    public async ValueTask<DaemonStartExecutionResult> Start (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonStart,
                projectPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStartExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var deadline = ExecutionDeadline.Start(executionContext.Timeout, timeProvider);
        var pluginLocateError = await VerifyUnityPluginWithinBudget(
                executionContext.Context.UnityProject.UnityProjectRoot,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (pluginLocateError != null)
        {
            return DaemonStartExecutionResult.Failure(pluginLocateError);
        }

        if (!deadline.TryGetRemainingTimeout(out var ensureRunningTimeout))
        {
            return DaemonStartExecutionResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor orchestration could begin."));
        }

        var startResult = await supervisorProjectGateway.EnsureRunning(
                executionContext.Context.UnityProject,
                ensureRunningTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!startResult.IsSuccess)
        {
            return DaemonStartExecutionResult.Failure(startResult.Error ?? ExecutionError.InternalError(
                "Daemon start operation failed without structured error details."));
        }

        if (!DaemonStartStateCodec.TryToValue(startResult.Status, out var startStatus))
        {
            return DaemonStartExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon start returned unsupported status: {startResult.Status}."));
        }

        var output = new DaemonStartExecutionOutput(
            StartStatus: startStatus!,
            DaemonStatus: DaemonStatusStateCodec.Running,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: daemonSessionOutputMapper.ToOutput(startResult.Session!));
        return DaemonStartExecutionResult.Success(output);
    }

    private async ValueTask<ExecutionError?> VerifyUnityPluginWithinBudget (
        string unityProjectRoot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var pluginLocateTimeout))
        {
            return ExecutionError.Timeout("Timed out before uCLI Unity plugin verification could begin.");
        }

        try
        {
            using var pluginLocateCancellationScope = TimeProviderCancellationScope.CreateLinked(
                cancellationToken,
                pluginLocateTimeout,
                timeProvider);
            var pluginLocateResult = await unityUcliPluginLocator.Locate(
                    unityProjectRoot,
                    pluginLocateCancellationScope.Token)
                .ConfigureAwait(false);
            return pluginLocateResult.IsSuccess
                ? null
                : pluginLocateResult.Error!;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ExecutionError.Timeout(
                $"Timed out while verifying the uCLI Unity plugin. Timeout={pluginLocateTimeout.TotalMilliseconds:0}ms.");
        }
    }
}
