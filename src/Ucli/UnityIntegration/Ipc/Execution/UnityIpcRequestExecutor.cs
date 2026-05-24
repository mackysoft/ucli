using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Orchestrates IPC requests through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
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
        TimeProvider? timeProvider = null)
    {
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.clientSelector = clientSelector ?? throw new ArgumentNullException(nameof(clientSelector));
        this.daemonReadinessGate = daemonReadinessGate ?? throw new ArgumentNullException(nameof(daemonReadinessGate));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        return await ExecuteCoreAsync(
                command,
                mode,
                timeout,
                config,
                unityProject,
                payload,
                onProgressFrame: null,
                cancellationToken)
            .ConfigureAwait(false);
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
        return await ExecuteCoreAsync(
                command,
                mode,
                timeout,
                config,
                unityProject,
                payload,
                onProgressFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> ExecuteCoreAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask>? onProgressFrame,
        CancellationToken cancellationToken)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var dispatchRequest = requestBuilder.Build(payload);
        if (onProgressFrame is not null)
        {
            dispatchRequest = dispatchRequest.WithResponseMode(IpcResponseModes.Stream);
        }

        var budget = UnityIpcExecutionBudget.Start(timeout, timeProvider);
        var targetResolution = await targetResolver.ResolveAsync(
                mode,
                unityProject,
                budget,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResolution.IsSuccess)
        {
            return UnityRequestExecutionResult.Failure(targetResolution.Failure!);
        }

        var unityIpcClient = clientSelector.Select(targetResolution.Target);
        if (onProgressFrame is null
            && targetResolution.Target == UnityExecutionTarget.Daemon
            && daemonReadinessGate.TryReadReadinessGatedOpsRead(dispatchRequest, out var opsReadRequest))
        {
            return await daemonReadinessGate.ExecuteAsync(
                    unityProject,
                    dispatchRequest,
                    opsReadRequest!,
                    budget,
                    unityIpcClient,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (onProgressFrame is not null
            && dispatchRequest.IsRecoverable)
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                $"Streaming IPC dispatch does not support recoverable request replay: {dispatchRequest.Method}."));
        }

        if (!budget.TryGetRemainingTimeout(out var requestTimeout))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                "Timed out before Unity IPC request dispatch could begin."));
        }

        if (onProgressFrame is not null)
        {
            return await unityIpcClient.SendStreamingAsync(
                    unityProject,
                    dispatchRequest,
                    requestTimeout,
                    (frame, progressCancellationToken) => onProgressFrame(
                        new UnityRequestProgressFrame(frame.Event!, frame.Payload),
                        progressCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await unityIpcClient.SendAsync(
                unityProject,
                dispatchRequest,
                requestTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
