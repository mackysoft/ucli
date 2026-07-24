using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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

        var unityIpcClient = clientSelector.Select(targetResolution.Target);
        if (targetResolution.Target == UnityExecutionTarget.Daemon
            && daemonReadinessGate.TryReadReadinessGatedOpsRead(dispatchRequest, out var opsReadRequest))
        {
            return await daemonReadinessGate.ExecuteAsync(
                    unityProject,
                    dispatchRequest,
                    opsReadRequest!,
                    deadline,
                    unityIpcClient,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                "Timed out before Unity IPC request dispatch could begin."));
        }

        return await unityIpcClient.SendAsync(
                unityProject,
                dispatchRequest,
                deadline,
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
}
