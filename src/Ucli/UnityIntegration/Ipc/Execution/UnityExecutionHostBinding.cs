using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary>
/// Caller-owned fixed delivery binding. It deliberately retains one selected IPC client and never
/// invokes target selection while starting or reconnecting a Lifecycle Execution.
/// </summary>
internal sealed class UnityExecutionHostBinding : IUnityExecutionHostBinding
{
    private readonly IUnityIpcClient? client;

    private readonly UnityIpcRequestBuilder requestBuilder;

    private readonly UnityDaemonReadinessGate? daemonReadinessGate;

    private readonly LifecycleExecutionStartBinding? reconnectStart;

    private readonly UnityIpcClientSelector? reconnectClientSelector;

    public UnityExecutionHostBinding (
        ResolvedUnityProjectContext project,
        UnityExecutionTarget target,
        IUnityIpcClient client,
        UnityIpcRequestBuilder requestBuilder,
        UnityDaemonReadinessGate daemonReadinessGate)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Target = target;
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        this.daemonReadinessGate = daemonReadinessGate ?? throw new ArgumentNullException(nameof(daemonReadinessGate));
        if (client.Target != target)
        {
            throw new ArgumentException(
                "The fixed Unity execution binding client must serve its selected target.",
                nameof(client));
        }
    }

    public UnityExecutionHostBinding (
        ResolvedUnityProjectContext project,
        LifecycleExecutionStartBinding reconnectStart,
        UnityIpcClientSelector reconnectClientSelector,
        UnityIpcRequestBuilder requestBuilder)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        this.reconnectStart = reconnectStart ?? throw new ArgumentNullException(nameof(reconnectStart));
        this.reconnectClientSelector = reconnectClientSelector ?? throw new ArgumentNullException(nameof(reconnectClientSelector));
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        Target = UnityExecutionTarget.Daemon;
    }

    public ResolvedUnityProjectContext Project { get; }

    public UnityExecutionTarget Target { get; }

    public async ValueTask<UnityRequestExecutionResult> StartAsync (
        UcliCommand command,
        UnityRequestPayload payload,
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(invocation);
        if (invocation.Context.HostBinding != this
            || invocation.Context.Project.UnityProject != Project)
        {
            throw new ArgumentException(
                "Lifecycle start invocation must use the fixed host binding that owns its project.",
                nameof(invocation));
        }

        var request = requestBuilder.Build(payload, invocation.StartObserver);
        if (!request.BeginsLifecycleExecution)
        {
            throw new ArgumentException(
                "A fixed-host Lifecycle Execution start requires a new Lifecycle dispatch payload.",
                nameof(payload));
        }
        if (invocation.ExecutionDeadline.IsExpired)
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.Timeout(
                    "Lifecycle Execution deadline expired before fixed-host action dispatch could begin."));
        }

        if (Target == UnityExecutionTarget.Daemon
            && request.StartAdmissionPolicy is not null)
        {
            return await daemonReadinessGate!.ExecuteLifecycleStartAdmissionAsync(
                    Project,
                    request,
                    request.StartAdmissionPolicy,
                    invocation.CallerWaitDeadline,
                    client!,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await client!.SendAsync(
                Project,
                request,
                invocation.CallerWaitDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<UnityRequestExecutionResult> ReconnectAsync (
        UcliCommand command,
        UnityRequestPayload payload,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(invocation);
        if (invocation.Context.HostBinding != this
            || invocation.Context.Project.UnityProject != Project)
        {
            throw new ArgumentException(
                "Lifecycle reconnect invocation must use the fixed host binding that owns its project.",
                nameof(invocation));
        }

        var request = requestBuilder.Build(payload);
        var requiredStart = request.RequiredStart
            ?? throw new ArgumentException(
                "A fixed-host Lifecycle Execution reconnect requires its persisted Start Record.",
                nameof(payload));
        if (requiredStart.LifecycleExecutionRef != invocation.ExecutionReference)
        {
            throw new ArgumentException(
                "Lifecycle reconnect invocation must use the persisted execution reference.",
                nameof(invocation));
        }

        if (reconnectStart is not null)
        {
            if (requiredStart != reconnectStart)
            {
                throw new ArgumentException("Reconnect binding requires its original durable Start Record.", nameof(payload));
            }

            return await reconnectClientSelector!.ReconnectAsync(
                    Project,
                    request,
                    requiredStart,
                    invocation.CallerWaitDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var attempt = await client!.TryReconnectAsync(
                Project,
                request,
                requiredStart,
                invocation.CallerWaitDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        return attempt.IsOwned
            ? attempt.Result!
            : UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.FromCodeAndMessage(
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity execution host did not prove ownership of the Lifecycle Execution."),
                requiredStart);
    }
}
