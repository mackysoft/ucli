using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents normalized output payload values for one Play Mode enter command execution. </summary>
internal sealed record PlayEnterExecutionOutput (
    ProjectIdentityInfo Project,
    ITerminalExecutionRef LifecycleExecutionRef,
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    UnityEditorMode EditorMode,
    UnityEditorLifecycleState? LifecycleState,
    UnityEditorBlockingReason? BlockingReason,
    UnityEditorCompileState? CompileState,
    UnityEditorGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    UnityEditorActionRequired? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    UnityEditorPlayModeSnapshot PlayMode,
    PlayTransitionSuccessOutput Transition,
    int TimeoutMilliseconds)
{
    public ITerminalExecutionRef LifecycleExecutionRef { get; } =
        LifecycleExecutionContractGuard.RequireCompletedTerminalReference(
            LifecycleExecutionRef,
            nameof(LifecycleExecutionRef),
            LifecycleExecutionKind.PlayEnter);

    public PlayTransitionSuccessOutput Transition { get; } =
        RequireSuccessfulTransition(Transition);

    private static PlayTransitionSuccessOutput RequireSuccessfulTransition (
        PlayTransitionSuccessOutput transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Transition != PlayLifecycleTransitionCommand.Enter
            || transition.Result is not PlayLifecycleTransitionOutcome.Entered
                and not PlayLifecycleTransitionOutcome.AlreadyEntered)
        {
            throw new ArgumentException(
                "Play enter success output requires a completed enter transition and its after snapshot.",
                nameof(transition));
        }

        return transition;
    }
}
