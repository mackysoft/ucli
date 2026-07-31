using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Selects one Unity IPC client by resolved execution target. </summary>
internal sealed class UnityIpcClientSelector
{
    private readonly IReadOnlyDictionary<UnityExecutionTarget, IUnityIpcClient> clientsByTarget;
    private readonly Func<ProcessIdentity, ProcessIdentityObservation> processIdentityObserver;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcClientSelector" /> class. </summary>
    /// <param name="unityIpcClients"> The registered Unity IPC clients. </param>
    public UnityIpcClientSelector (IEnumerable<IUnityIpcClient> unityIpcClients)
        : this(
            unityIpcClients,
            ProcessLivenessProbe.ObserveIdentity)
    {
    }

    internal UnityIpcClientSelector (
        IEnumerable<IUnityIpcClient> unityIpcClients,
        Func<ProcessIdentity, ProcessIdentityObservation>
            processIdentityObserver)
    {
        ArgumentNullException.ThrowIfNull(unityIpcClients);
        ArgumentNullException.ThrowIfNull(processIdentityObserver);

        var clients = new Dictionary<UnityExecutionTarget, IUnityIpcClient>();
        foreach (var unityIpcClient in unityIpcClients)
        {
            if (unityIpcClient == null)
            {
                throw new ArgumentException("Unity IPC clients must not contain null entries.", nameof(unityIpcClients));
            }

            if (!clients.TryAdd(unityIpcClient.Target, unityIpcClient))
            {
                throw new InvalidOperationException($"Multiple Unity IPC clients were registered for '{unityIpcClient.Target}'.");
            }
        }

        clientsByTarget = clients;
        this.processIdentityObserver = processIdentityObserver;
    }

    /// <summary> Selects the client registered for the specified execution target. </summary>
    /// <param name="target"> The resolved execution target. </param>
    /// <returns> The matching Unity IPC client. </returns>
    public IUnityIpcClient Select (UnityExecutionTarget target)
    {
        if (!clientsByTarget.TryGetValue(target, out var client))
        {
            throw new InvalidOperationException($"Unity IPC client for target '{target}' is not registered.");
        }

        return client;
    }

    /// <summary>
    /// Reconnects through the existing daemon or oneshot provider that proves the persisted host.
    /// No target decision or process launch occurs on this path.
    /// </summary>
    public async ValueTask<UnityRequestExecutionResult> ReconnectAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(requiredStart);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        if (processIdentityObserver(requiredStart.Host.Process)
            == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
        {
            return CreateConfirmedHostExitResult(requiredStart);
        }

        foreach (var target in new[]
                 {
                     UnityExecutionTarget.Daemon,
                     UnityExecutionTarget.Oneshot,
                 })
        {
            var attempt = await Select(target)
                .TryReconnectAsync(
                    unityProject,
                    dispatchRequest,
                    requiredStart,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (attempt.IsOwned)
            {
                return attempt.Result!;
            }
        }

        if (processIdentityObserver(requiredStart.Host.Process)
            == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
        {
            return CreateConfirmedHostExitResult(requiredStart);
        }

        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "No existing Unity IPC provider endpoint proved ownership of the Lifecycle Execution host."),
            requiredStart);
    }

    private static UnityRequestExecutionResult CreateConfirmedHostExitResult (
        LifecycleExecutionStartBinding requiredStart)
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "The Unity Editor process that owns the Lifecycle Execution is no longer running."),
            requiredStart,
            lifecycleActionDispatched: false,
            new LifecycleExecutionHostExitObservation(
                requiredStart.Host.Process));
    }
}
