using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
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

    private readonly DaemonSession? fixedDaemonSession;

    private OneshotHostLease? fixedOneshotLease;

    private readonly bool hasFixedOneshotLease;

    public UnityExecutionHostBinding (
        ResolvedUnityProjectContext project,
        UnityExecutionTarget target,
        IUnityIpcClient client,
        UnityIpcRequestBuilder requestBuilder,
        UnityDaemonReadinessGate daemonReadinessGate,
        DaemonSession? fixedDaemonSession = null,
        OneshotHostLease? fixedOneshotLease = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Target = target;
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        this.daemonReadinessGate = daemonReadinessGate ?? throw new ArgumentNullException(nameof(daemonReadinessGate));
        this.fixedDaemonSession = fixedDaemonSession;
        this.fixedOneshotLease = fixedOneshotLease;
        hasFixedOneshotLease = fixedOneshotLease is not null;
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

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityRequestPayload payload,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(deadline);
        if (reconnectStart is not null)
        {
            throw new InvalidOperationException(
                "A Lifecycle reconnect binding cannot execute a new Program registration request.");
        }

        var request = requestBuilder.Build(payload);
        if (!UnityIpcMethodCapabilities.SupportsStatelessReadReplay(request.Method)
            && request.Method is not UnityIpcMethod.ProgramRequestStart
            && request.Method is not UnityIpcMethod.ProgramRequestAttach
            && request.Method is not UnityIpcMethod.ProgramRequestCancel)
        {
            throw new ArgumentException(
                "A fixed-host general request must be a stateless read or a Program-owned logical Request execution.",
                nameof(payload));
        }

        if (fixedDaemonSession is not null)
        {
            return await ((UnityDaemonIpcClient)client!).SendBoundAsync(
                    Project,
                    request,
                    deadline,
                    fixedDaemonSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await client!.SendAsync(Project, request, deadline, cancellationToken)
            .ConfigureAwait(false);
    }

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

        if (fixedDaemonSession is not null)
        {
            return await ((UnityDaemonIpcClient)client!).SendBoundAsync(
                    Project,
                    request,
                    invocation.CallerWaitDeadline,
                    fixedDaemonSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (hasFixedOneshotLease)
        {
            var lease = Interlocked.Exchange(ref fixedOneshotLease, null)
                ?? throw new InvalidOperationException(
                    "The fixed oneshot host binding may start its Lifecycle Execution only once.");
            return await ((UnityOneshotIpcClient)client!).SendBoundAsync(
                    Project,
                    request,
                    invocation.CallerWaitDeadline,
                    lease,
                    cancellationToken)
                .ConfigureAwait(false);
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync ()
    {
        var lease = Interlocked.Exchange(ref fixedOneshotLease, null);
        if (lease is not null)
        {
            await ((UnityOneshotIpcClient)client!).DisposeBoundLeaseAsync(lease)
                .ConfigureAwait(false);
        }
    }
}
