using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Owns the Play Mode entry-specific transition contract. </summary>
internal sealed class PlayEnterTransitionDirectionPolicy :
    IPlayTransitionDirectionPolicy<PlayEnterExecutionOutput>
{
    private static readonly LifecycleExecutionDefinition ExecutionDefinition =
        new(LifecycleExecutionKind.PlayEnter);

    private PlayEnterTransitionDirectionPolicy ()
    {
    }

    public static PlayEnterTransitionDirectionPolicy Instance { get; } = new();

    public UcliCommand Command => UcliCommandIds.PlayEnter;

    public LifecycleExecutionDefinition Definition => ExecutionDefinition;

    public PlayLifecycleTransitionCommand Transition =>
        PlayLifecycleTransitionCommand.Enter;

    public UcliCode ActionRejectedCode =>
        PlayModeErrorCodes.PlayModeEnterRejected;

    public string ActionDisplayName => "Play Mode entry";

    public string CommandDisplayName => "Play Mode enter";

    public string SessionNotAvailableMessage =>
        "Registered GUI daemon session is not available for Play Mode enter.";

    public string RequiresGuiEditorMessage =>
        "Play Mode enter requires a registered GUI daemon session.";

    public UnityRequestPayload CreatePayload (
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? requiredStart)
    {
        return new UnityRequestPayload.PlayEnter(
            registration,
            requiredStart);
    }

    public LifecycleExecutionTerminalRecord CreateHostExitTerminalRecord (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalFacts terminalFacts)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new PlayEnterLifecycleExecutionTerminalRecord(
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            terminalFacts.CompletedAtUtc,
            terminalFacts.TerminalReason,
            terminalFacts.ApplicationState,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
    }

    public bool TryGetTerminalResult (
        LifecycleExecutionTerminalRecord terminalRecord,
        out PlayLifecycleTransitionResult? result)
    {
        if (terminalRecord
            is PlayEnterLifecycleExecutionTerminalRecord playEnterRecord)
        {
            result = playEnterRecord.Result;
            return true;
        }

        result = null;
        return false;
    }

    public ApplicationFailure? ValidateTransitionSnapshots (
        PlayLifecycleTransitionResult transition,
        UnityEditorObservation currentSnapshot)
    {
        return transition.Result switch
        {
            PlayLifecycleTransitionOutcome.Entered =>
                ValidateEntered(transition.Before, currentSnapshot),
            PlayLifecycleTransitionOutcome.AlreadyEntered =>
                ValidateAlreadyEntered(transition.Before, currentSnapshot),
            PlayLifecycleTransitionOutcome.Timeout
                or PlayLifecycleTransitionOutcome.Blocked => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition.Result,
                null),
        };
    }

    public bool IsSuccessfulOutcome (
        PlayLifecycleTransitionOutcome outcome)
    {
        return outcome is PlayLifecycleTransitionOutcome.Entered
            or PlayLifecycleTransitionOutcome.AlreadyEntered;
    }

    public PlayEnterExecutionOutput CreateOutput (
        PlayCommandExecutionContext context,
        ITerminalExecutionRef terminalExecutionRef,
        PlayLifecycleSnapshotOutput lifecycle,
        PlayTransitionSuccessOutput transition)
    {
        return new PlayEnterExecutionOutput(
            Project: context.Project,
            LifecycleExecutionRef: terminalExecutionRef,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: UnityEditorMode.Gui,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            Generations: lifecycle.Generations,
            CanAcceptExecutionRequests:
                lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: lifecycle.PrimaryDiagnostic,
            PlayMode: lifecycle.PlayMode,
            Transition: transition,
            TimeoutMilliseconds: context.TimeoutMilliseconds);
    }

    private static ApplicationFailure? ValidateEntered (
        UnityEditorObservation before,
        UnityEditorObservation after)
    {
        if (!IsReadyStoppedSnapshot(before))
        {
            return CreateStateUnknownFailure(
                "Unity play enter reported entered without a ready stopped before snapshot.");
        }

        if (!IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure(
                "Unity play enter reported entered without a playing snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration
            == after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure(
                "Unity play enter reported entered without changing generations.playModeGeneration.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateAlreadyEntered (
        UnityEditorObservation before,
        UnityEditorObservation after)
    {
        if (!IsEnteredSnapshot(before) || !IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure(
                "Unity play enter reported alreadyEntered without a playing snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration
            != after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure(
                "Unity play enter reported alreadyEntered after changing generations.playModeGeneration.");
        }

        return null;
    }

    private static bool IsReadyStoppedSnapshot (
        UnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        var playMode = state.PlayMode;
        return state.LifecycleState == UnityEditorLifecycleState.Ready
            && playMode.State == UnityEditorPlayModeState.Stopped
            && playMode.Transition == UnityEditorPlayModeTransition.None
            && !playMode.IsPlaying
            && !playMode.IsPlayingOrWillChangePlaymode;
    }

    private static bool IsEnteredSnapshot (
        UnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        var playMode = state.PlayMode;
        return state.LifecycleState == UnityEditorLifecycleState.PlayMode
            && playMode.State == UnityEditorPlayModeState.Playing
            && playMode.Transition == UnityEditorPlayModeTransition.None
            && playMode.IsPlaying;
    }

    private static ApplicationFailure CreateStateUnknownFailure (
        string message)
    {
        return ApplicationFailure.InternalError(
            message,
            PlayModeErrorCodes.PlayModeStateUnknown);
    }
}
