using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents lifecycle evidence shared by errors that retain a typed Play Mode transition result.
/// </summary>
internal abstract record PlayTransitionEvidenceErrorCommandPayload<
    TExecutionRef,
    TTransition> : PlayTransitionErrorCommandPayload
    where TExecutionRef : class
    where TTransition : class
{
    protected PlayTransitionEvidenceErrorCommandPayload (
        ProjectIdentityInfo project,
        TExecutionRef lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        PlayLifecycleSnapshotOutput lifecycle,
        TTransition transition,
        int timeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode application state must be defined.");
        }
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode transitions do not support a partially applied state.");
        }
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                timeoutMilliseconds,
                "Play Mode transition timeout must be positive.");
        }

        Project = project ?? throw new ArgumentNullException(nameof(project));
        LifecycleExecutionRef = lifecycleExecutionRef
            ?? throw new ArgumentNullException(nameof(lifecycleExecutionRef));
        ApplicationState = applicationState;
        DaemonStatus = DaemonStatusKind.Running;
        ServerVersion = lifecycle.ServerVersion;
        EditorMode = lifecycle.EditorMode;
        LifecycleState = lifecycle.LifecycleState;
        BlockingReason = lifecycle.BlockingReason;
        CompileState = lifecycle.CompileState;
        Generations = lifecycle.Generations;
        CanAcceptExecutionRequests =
            lifecycle.CanAcceptExecutionRequests;
        ObservedAtUtc = lifecycle.ObservedAtUtc;
        ActionRequired = lifecycle.ActionRequired;
        PrimaryDiagnostic = lifecycle.PrimaryDiagnostic;
        PlayMode = lifecycle.PlayMode
            ?? throw new ArgumentException(
                "Typed Play Mode transition evidence requires a Play Mode snapshot.",
                nameof(lifecycle));
        Transition = transition
            ?? throw new ArgumentNullException(nameof(transition));
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    public ProjectIdentityInfo Project { get; }

    public TExecutionRef LifecycleExecutionRef { get; }

    public ExecutionApplicationState ApplicationState { get; }

    public DaemonStatusKind DaemonStatus { get; }

    public string? ServerVersion { get; }

    public UnityEditorMode? EditorMode { get; }

    public UnityEditorLifecycleState? LifecycleState { get; }

    public UnityEditorBlockingReason? BlockingReason { get; }

    public UnityEditorCompileState? CompileState { get; }

    public UnityEditorGenerationSnapshot? Generations { get; }

    public bool CanAcceptExecutionRequests { get; }

    public DateTimeOffset? ObservedAtUtc { get; }

    public UnityEditorActionRequired? ActionRequired { get; }

    public DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic { get; }

    public UnityEditorPlayModeSnapshot PlayMode { get; }

    public TTransition Transition { get; }

    public int TimeoutMilliseconds { get; }
}
