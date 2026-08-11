using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Orchestrates IPC requests through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor,
    ILifecycleExecutionHostBindingFactory
{
    private readonly UnityIpcRequestBuilder requestBuilder;

    private readonly UnityIpcExecutionTargetResolver targetResolver;

    private readonly UnityIpcClientSelector clientSelector;

    private readonly UnityDaemonReadinessGate daemonReadinessGate;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestExecutor" /> class. </summary>
    /// <param name="requestBuilder"> The application-payload to IPC-dispatch request builder dependency. </param>
    /// <param name="targetResolver"> The execution target resolver dependency. </param>
    /// <param name="clientSelector"> The IPC client selector dependency. </param>
    /// <param name="daemonReadinessGate"> The daemon readiness gate dependency. </param>
    /// <param name="timeProvider"> The time provider used to measure the shared timeout budget. </param>
    public UnityIpcRequestExecutor (
        UnityIpcRequestBuilder requestBuilder,
        UnityIpcExecutionTargetResolver targetResolver,
        UnityIpcClientSelector clientSelector,
        UnityDaemonReadinessGate daemonReadinessGate,
        TimeProvider timeProvider)
    {
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.clientSelector = clientSelector ?? throw new ArgumentNullException(nameof(clientSelector));
        this.daemonReadinessGate = daemonReadinessGate ?? throw new ArgumentNullException(nameof(daemonReadinessGate));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        ValidateExecutionInputs(command, timeout, config, unityProject, payload, cancellationToken);
        var dispatchRequest = requestBuilder.Build(payload);
        if (!TryCreateRequestDeadline(
                dispatchRequest,
                timeout,
                out var deadline))
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.Timeout(
                    "Lifecycle Execution deadline expired before Unity target resolution."));
        }

        if (dispatchRequest.RequiredStart is not null)
        {
            return await clientSelector.ReconnectAsync(
                    unityProject,
                    dispatchRequest,
                    dispatchRequest.RequiredStart,
                    deadline!,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var targetResolution = await targetResolver.ResolveAsync(
                mode,
                unityProject,
                deadline!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResolution.IsSuccess)
        {
            return UnityRequestExecutionResult.Failure(targetResolution.Failure!);
        }

        var unityIpcClient = clientSelector.Select(targetResolution.Target);
        if (targetResolution.Target == UnityExecutionTarget.Daemon
            && dispatchRequest.StartAdmissionPolicy is not null)
        {
            return await daemonReadinessGate.ExecuteLifecycleStartAdmissionAsync(
                    unityProject,
                    dispatchRequest,
                    dispatchRequest.StartAdmissionPolicy,
                    deadline!,
                    unityIpcClient,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (targetResolution.Target == UnityExecutionTarget.Daemon
            && daemonReadinessGate.TryReadReadinessGatedOpsRead(dispatchRequest, out var opsReadRequest))
        {
            return await daemonReadinessGate.ExecuteAsync(
                    unityProject,
                    dispatchRequest,
                    opsReadRequest!,
                    deadline!,
                    unityIpcClient,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!deadline!.TryGetRemainingTimeout(out _))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                "Timed out before Unity IPC request dispatch could begin."));
        }

        return await unityIpcClient.SendAsync(
                unityProject,
                dispatchRequest,
                deadline!,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (
        UnityExecutionMode requestedMode,
        ResolvedUnityProjectContext project,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(executionDeadline);
        cancellationToken.ThrowIfCancellationRequested();

        var resolution = await targetResolver.ResolveAsync(
                requestedMode,
                project,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!resolution.IsSuccess)
        {
            return LifecycleExecutionHostBindingResolution.FromFailure(
                resolution.Failure!);
        }

        return await BindTargetAsync(
                project,
                resolution.Target,
                executionDeadline,
                verifyOneshotPlugin: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<LifecycleExecutionHostBindingResolution> BindResolvedTargetAsync (
        ResolvedUnityProjectContext project,
        UnityExecutionTarget target,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default)
    {
        return await BindTargetAsync(
                project,
                target,
                executionDeadline,
                verifyOneshotPlugin: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<LifecycleExecutionHostBindingResolution> BindTargetAsync (
        ResolvedUnityProjectContext project,
        UnityExecutionTarget target,
        ExecutionDeadline executionDeadline,
        bool verifyOneshotPlugin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(executionDeadline);
        cancellationToken.ThrowIfCancellationRequested();

        if (verifyOneshotPlugin && target == UnityExecutionTarget.Oneshot)
        {
            var pluginFailure = await targetResolver.VerifyOneshotPluginAsync(
                    project,
                    executionDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pluginFailure != null)
            {
                return LifecycleExecutionHostBindingResolution.FromFailure(pluginFailure);
            }
        }

        var client = clientSelector.Select(target);
        if (client is UnityOneshotIpcClient oneshotClient)
        {
            var hostBinding = await oneshotClient.BindHostAsync(
                    project,
                    executionDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!hostBinding.IsSuccess)
            {
                return LifecycleExecutionHostBindingResolution.FromFailure(
                    hostBinding.Failure!);
            }

            return LifecycleExecutionHostBindingResolution.Success(
                new UnityExecutionHostBinding(
                    project,
                    target,
                    client,
                    requestBuilder,
                    daemonReadinessGate,
                    fixedOneshotLease: hostBinding.Lease));
        }

        if (client is UnityDaemonIpcClient daemonClient)
        {
            var hostBinding = await daemonClient.BindHostAsync(
                    project,
                    executionDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!hostBinding.IsSuccess)
            {
                return LifecycleExecutionHostBindingResolution.FromFailure(
                    hostBinding.Failure!);
            }

            return LifecycleExecutionHostBindingResolution.Success(
                new UnityExecutionHostBinding(
                    project,
                    target,
                    client,
                    requestBuilder,
                    daemonReadinessGate,
                    hostBinding.Session));
        }

        return LifecycleExecutionHostBindingResolution.Success(
            new UnityExecutionHostBinding(
                project,
                target,
                client,
                requestBuilder,
                daemonReadinessGate));
    }

    /// <inheritdoc />
    public ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline callerWaitDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(requiredStart);
        ArgumentNullException.ThrowIfNull(callerWaitDeadline);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LifecycleExecutionHostBindingResolution.Success(
            new UnityExecutionHostBinding(
                project,
                requiredStart,
                clientSelector,
                requestBuilder)));
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ValidateExecutionInputs(command, timeout, config, unityProject, payload, cancellationToken);

        var dispatchRequest = requestBuilder.Build(payload);
        if (!UnityIpcMethodCapabilities.SupportsStreaming(dispatchRequest.Method))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                $"IPC method does not support streaming: {TextVocabulary.GetText(dispatchRequest.Method)}."));
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var targetResolution = await targetResolver.ResolveAsync(
                mode,
                unityProject,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResolution.IsSuccess)
        {
            return UnityRequestExecutionResult.Failure(targetResolution.Failure!);
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                "Timed out before Unity IPC request dispatch could begin."));
        }

        var unityIpcClient = clientSelector.Select(targetResolution.Target);
        return await unityIpcClient.SendStreamingAsync(
                unityProject,
                dispatchRequest,
                deadline,
                (frame, progressCancellationToken) => onProgressFrame(
                    new UnityRequestProgressFrame(frame.Event!, frame.Payload),
                    progressCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateExecutionInputs (
        UcliCommand command,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool TryCreateRequestDeadline (
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        out ExecutionDeadline? deadline)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        if (!dispatchRequest.BeginsLifecycleExecution)
        {
            deadline = ExecutionDeadline.Start(timeout, timeProvider);
            return true;
        }

        return ExecutionDeadline.TryStartUntil(
            dispatchRequest.Registration!.DeadlineUtc,
            timeProvider,
            out deadline);
    }
}
