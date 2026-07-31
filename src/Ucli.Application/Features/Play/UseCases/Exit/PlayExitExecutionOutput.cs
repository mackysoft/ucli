using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Represents normalized output payload values for one Play Mode exit command execution. </summary>
internal sealed record PlayExitExecutionOutput (
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
            LifecycleExecutionKind.PlayExit);

    public PlayTransitionSuccessOutput Transition { get; } =
        RequireSuccessfulTransition(Transition);

    private static PlayTransitionSuccessOutput RequireSuccessfulTransition (
        PlayTransitionSuccessOutput transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Transition != PlayLifecycleTransitionCommand.Exit
            || transition.Result is not PlayLifecycleTransitionOutcome.Exited
                and not PlayLifecycleTransitionOutcome.AlreadyExited)
        {
            throw new ArgumentException(
                "Play exit success output requires a completed exit transition and its after snapshot.",
                nameof(transition));
        }

        return transition;
    }
}
