using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Preflight;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Implements daemon-start command workflow orchestration. </summary>
internal sealed class DaemonStartService : IDaemonStartService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonProjectLifecycleGateway daemonProjectLifecycleGateway;

    private readonly IUnityPluginVerifier unityPluginVerifier;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    private readonly IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonProjectLifecycleGateway"> The daemon project-lifecycle gateway dependency. </param>
    /// <param name="unityPluginVerifier"> The Unity uCLI plugin-verifier dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <param name="daemonDiagnosisOutputMapper"> The daemon diagnosis-output mapper dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonProjectLifecycleGateway daemonProjectLifecycleGateway,
        IUnityPluginVerifier unityPluginVerifier,
        IDaemonSessionOutputMapper daemonSessionOutputMapper,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonProjectLifecycleGateway = daemonProjectLifecycleGateway ?? throw new ArgumentNullException(nameof(daemonProjectLifecycleGateway));
        this.unityPluginVerifier = unityPluginVerifier ?? throw new ArgumentNullException(nameof(unityPluginVerifier));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
        this.daemonDiagnosisOutputMapper = daemonDiagnosisOutputMapper ?? throw new ArgumentNullException(nameof(daemonDiagnosisOutputMapper));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="editorMode"> The optional normalized <c>--editorMode</c> value. </param>
    /// <param name="onStartupBlocked"> The normalized <c>--onStartupBlocked</c> value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    public async ValueTask<DaemonStartExecutionResult> StartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                UcliCommandIds.DaemonStart,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStartExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var deadline = ExecutionDeadline.Start(executionContext.Timeout, timeProvider);
        var pluginLocateError = await VerifyUnityPluginWithinBudgetAsync(
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
                "Timed out before daemon lifecycle orchestration could begin."));
        }

        var startResult = await daemonProjectLifecycleGateway.EnsureRunningAsync(
                executionContext.Context.UnityProject,
                ensureRunningTimeout,
                editorMode,
                onStartupBlocked,
                cancellationToken)
            .ConfigureAwait(false);
        if (!startResult.IsSuccess)
        {
            var diagnosis = startResult.Diagnosis is null
                ? null
                : daemonDiagnosisOutputMapper.ToOutput(startResult.Diagnosis);
            return DaemonStartExecutionResult.Failure(startResult.Error ?? ExecutionError.InternalError(
                "Daemon start operation failed without structured error details."), diagnosis, startResult.Startup);
        }

        var output = new DaemonStartExecutionOutput(
            StartStatus: startResult.Status,
            DaemonStatus: DaemonStatusKind.Running,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: daemonSessionOutputMapper.ToOutput(startResult.Session!));
        return DaemonStartExecutionResult.Success(output);
    }

    private async ValueTask<ExecutionError?> VerifyUnityPluginWithinBudgetAsync (
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
            var pluginLocateResult = await unityPluginVerifier.VerifyAsync(
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
