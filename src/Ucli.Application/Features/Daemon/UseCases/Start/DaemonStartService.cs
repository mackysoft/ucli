using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Preflight;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
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
    /// <param name="progressSink"> The optional command-neutral sink that receives host-visible daemon-start progress entries. When <see langword="null" />, no progress entries are emitted. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    public async ValueTask<DaemonStartExecutionResult> StartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ICommandProgressSink? progressSink = null,
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
        var effectiveTimeoutMilliseconds = checked((int)executionContext.Timeout.TotalMilliseconds);
        var progressEmitter = new DaemonStartProgressEmitter(
            progressSink,
            executionContext.Context.UnityProject.ProjectFingerprint,
            effectiveTimeoutMilliseconds,
            editorMode,
            onStartupBlocked);
        var timeoutBudget = ExecutionTimeoutBudget.Start(executionContext.Timeout, timeProvider);
        await EmitProgressOutsideBudgetAsync(timeoutBudget, progressEmitter.EmitStartedAsync, cancellationToken).ConfigureAwait(false);
        await EmitProgressOutsideBudgetAsync(timeoutBudget, progressEmitter.EmitPluginVerificationStartedAsync, cancellationToken).ConfigureAwait(false);

        var pluginLocateError = !timeoutBudget.TryGetRemainingTimeout(out var pluginLocateTimeout)
            ? ExecutionError.Timeout("Timed out before uCLI Unity plugin verification could begin.")
            : await VerifyUnityPluginWithinBudgetAsync(
                    executionContext.Context.UnityProject.UnityProjectRoot,
                    pluginLocateTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                token => progressEmitter.EmitPluginVerificationCompletedAsync(pluginLocateError, token),
                cancellationToken)
            .ConfigureAwait(false);
        if (pluginLocateError != null)
        {
            var failure = DaemonStartExecutionResult.Failure(
                pluginLocateError,
                DaemonStartFailureExecutionOutput.Create(
                    DaemonStatusKind.NotRunning,
                    effectiveTimeoutMilliseconds,
                    startup: null,
                    diagnosis: null));
            await EmitProgressOutsideBudgetAsync(
                    timeoutBudget,
                    token => progressEmitter.EmitCompletedAsync(DaemonStartStatus.Failed, failure.FailureOutput!.DaemonStatus, failure.Error, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return failure;
        }

        if (!timeoutBudget.TryGetRemainingTimeout(out var ensureRunningTimeout))
        {
            var failure = DaemonStartExecutionResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon lifecycle orchestration could begin."),
                DaemonStartFailureExecutionOutput.Create(
                    DaemonStatusKind.NotRunning,
                    effectiveTimeoutMilliseconds,
                    startup: null,
                    diagnosis: null));
            await EmitProgressOutsideBudgetAsync(
                    timeoutBudget,
                    token => progressEmitter.EmitCompletedAsync(DaemonStartStatus.Failed, failure.FailureOutput!.DaemonStatus, failure.Error, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return failure;
        }

        var startResult = await daemonProjectLifecycleGateway.EnsureRunningAsync(
                executionContext.Context.UnityProject,
                ensureRunningTimeout,
                editorMode,
                onStartupBlocked,
                progressEmitter,
                progressSink,
                cancellationToken)
            .ConfigureAwait(false);
        if (!startResult.IsSuccess)
        {
            var diagnosis = startResult.Diagnosis is null
                ? null
                : daemonDiagnosisOutputMapper.ToOutput(startResult.Diagnosis);
            var failure = DaemonStartExecutionResult.Failure(startResult.Error ?? ExecutionError.InternalError(
                "Daemon start operation failed without structured error details."),
                DaemonStartFailureExecutionOutput.Create(
                    startResult.DaemonStatus,
                    effectiveTimeoutMilliseconds,
                    startResult.Startup,
                    diagnosis));
            await EmitProgressOutsideBudgetAsync(
                    timeoutBudget,
                    token => progressEmitter.EmitCompletedAsync(DaemonStartStatus.Failed, failure.FailureOutput!.DaemonStatus, failure.Error, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return failure;
        }

        var lifecycleSnapshot = startResult.LifecycleSnapshot ?? DaemonStartLifecycleSnapshot.Ready();
        var output = new DaemonStartExecutionOutput(
            StartStatus: startResult.Status,
            DaemonStatus: DaemonStatusKind.Running,
            TimeoutMilliseconds: effectiveTimeoutMilliseconds,
            Session: daemonSessionOutputMapper.ToOutput(startResult.Session!),
            LifecycleState: lifecycleSnapshot.LifecycleState,
            BlockingReason: lifecycleSnapshot.BlockingReason,
            CanAcceptExecutionRequests: lifecycleSnapshot.CanAcceptExecutionRequests);
        var success = DaemonStartExecutionResult.Success(output);
        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                token => progressEmitter.EmitCompletedAsync(output.StartStatus, output.DaemonStatus, error: null, cancellationToken: token),
                cancellationToken)
            .ConfigureAwait(false);
        return success;
    }

    private async ValueTask<ExecutionError?> VerifyUnityPluginWithinBudgetAsync (
        string unityProjectRoot,
        TimeSpan pluginLocateTimeout,
        CancellationToken cancellationToken)
    {
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

    private static async ValueTask EmitProgressOutsideBudgetAsync (
        ExecutionTimeoutBudget timeoutBudget,
        Func<CancellationToken, ValueTask> emit,
        CancellationToken cancellationToken)
    {
        using var excludedSection = timeoutBudget.BeginExcludedSection();
        await emit(cancellationToken).ConfigureAwait(false);
    }
}
