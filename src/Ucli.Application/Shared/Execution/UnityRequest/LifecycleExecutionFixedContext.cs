using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary>
/// Captures the project, requested mode, and one caller-owned delivery binding selected at the
/// beginning of a Lifecycle Execution. Actions must use this binding rather than resolving a
/// later host.
/// </summary>
internal sealed class LifecycleExecutionFixedContext
{
    public LifecycleExecutionFixedContext (
        ProjectContext project,
        ExecutionMode requestedMode,
        IUnityExecutionHostBinding hostBinding)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        RequestedMode = requestedMode;
        HostBinding = hostBinding ?? throw new ArgumentNullException(nameof(hostBinding));
        if (HostBinding.Project != Project.UnityProject)
        {
            throw new ArgumentException(
                "A Lifecycle Execution host binding must belong to its fixed project.",
                nameof(hostBinding));
        }
    }

    public ProjectContext Project { get; }

    public ExecutionMode RequestedMode { get; }

    public IUnityExecutionHostBinding HostBinding { get; }
}

/// <summary>
/// Describes a new Lifecycle Execution dispatch on a fixed host binding. The caller that receives
/// this invocation owns the binding lifetime until <see cref="IUnityExecutionHostBinding.StartAsync" />
/// atomically transfers an accepted oneshot lease; action services never dispose it.
/// </summary>
internal sealed class LifecycleExecutionStartInvocation
{
    public LifecycleExecutionStartInvocation (
        LifecycleExecutionFixedContext context,
        ExecutionDeadline executionDeadline,
        ExecutionDeadline callerWaitDeadline,
        ILifecycleExecutionStartObserver startObserver)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ExecutionDeadline = executionDeadline ?? throw new ArgumentNullException(nameof(executionDeadline));
        CallerWaitDeadline = callerWaitDeadline ?? throw new ArgumentNullException(nameof(callerWaitDeadline));
        StartObserver = startObserver ?? throw new ArgumentNullException(nameof(startObserver));
    }

    public LifecycleExecutionFixedContext Context { get; }

    public ExecutionDeadline ExecutionDeadline { get; }

    public ExecutionDeadline CallerWaitDeadline { get; }

    public ILifecycleExecutionStartObserver StartObserver { get; }
}

/// <summary> Describes reconnection to one already published Lifecycle Execution on its fixed host. </summary>
internal sealed class LifecycleExecutionReconnectInvocation
{
    public LifecycleExecutionReconnectInvocation (
        LifecycleExecutionFixedContext context,
        ExecutionRef executionReference,
        ExecutionDeadline callerWaitDeadline)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ExecutionReference = executionReference ?? throw new ArgumentNullException(nameof(executionReference));
        CallerWaitDeadline = callerWaitDeadline ?? throw new ArgumentNullException(nameof(callerWaitDeadline));
    }

    public LifecycleExecutionFixedContext Context { get; }

    public ExecutionRef ExecutionReference { get; }

    public ExecutionDeadline CallerWaitDeadline { get; }
}

/// <summary> Owns delivery through exactly one project and Unity execution target. </summary>
internal interface IUnityExecutionHostBinding : IAsyncDisposable
{
    /// <summary> Gets the project fixed when this binding was created. </summary>
    ResolvedUnityProjectContext Project { get; }

    /// <summary> Gets the selected provider target. </summary>
    UnityExecutionTarget Target { get; }

    /// <summary> Starts one Lifecycle Execution without resolving another host. </summary>
    ValueTask<UnityRequestExecutionResult> StartAsync (
        UcliCommand command,
        UnityRequestPayload payload,
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects through this binding only; it never falls back to another host. </summary>
    ValueTask<UnityRequestExecutionResult> ReconnectAsync (
        UcliCommand command,
        UnityRequestPayload payload,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default);
}

/// <summary> Resolves one fixed host binding before a Lifecycle Execution receives its registration. </summary>
internal interface ILifecycleExecutionHostBindingFactory
{
    /// <summary> Resolves the single host binding for a new Lifecycle Execution. </summary>
    ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (
        ExecutionMode requestedMode,
        ResolvedUnityProjectContext project,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default);

    /// <summary> Binds an action policy's already resolved target without re-evaluating execution mode. </summary>
    ValueTask<LifecycleExecutionHostBindingResolution> BindResolvedTargetAsync (
        ResolvedUnityProjectContext project,
        UnityExecutionTarget target,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a recovery binding which proves only the host recorded by a durable Start Record.
    /// It must not resolve a new target or launch another Unity process.
    /// </summary>
    ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline callerWaitDeadline,
        CancellationToken cancellationToken = default);
}

/// <summary> Carries either a fixed host context or the typed failure that prevented binding. </summary>
internal sealed record LifecycleExecutionHostBindingResolution
{
    private LifecycleExecutionHostBindingResolution (
        IUnityExecutionHostBinding? binding,
        UnityRequestFailure? failure)
    {
        Binding = binding;
        Failure = failure;
    }

    public IUnityExecutionHostBinding? Binding { get; }

    public UnityRequestFailure? Failure { get; }

    public bool IsSuccess => Binding is not null;

    public static LifecycleExecutionHostBindingResolution Success (
        IUnityExecutionHostBinding binding)
    {
        return new LifecycleExecutionHostBindingResolution(
            binding ?? throw new ArgumentNullException(nameof(binding)),
            failure: null);
    }

    public static LifecycleExecutionHostBindingResolution FromFailure (
        UnityRequestFailure failure)
    {
        return new LifecycleExecutionHostBindingResolution(
            binding: null,
            failure ?? throw new ArgumentNullException(nameof(failure)));
    }
}
