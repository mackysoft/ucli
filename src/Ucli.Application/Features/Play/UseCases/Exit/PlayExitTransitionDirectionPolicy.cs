using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Owns the Play Mode exit-specific transition contract. </summary>
internal sealed class PlayExitTransitionDirectionPolicy :
    IPlayTransitionDirectionPolicy<PlayExitExecutionOutput>
{
    private static readonly LifecycleExecutionDefinition ExecutionDefinition =
        new(LifecycleExecutionKind.PlayExit);

    private PlayExitTransitionDirectionPolicy ()
    {
    }

    public static PlayExitTransitionDirectionPolicy Instance { get; } = new();

    public UcliCommand Command => UcliCommandIds.PlayExit;

    public LifecycleExecutionDefinition Definition => ExecutionDefinition;

    public PlayLifecycleTransitionCommand Transition =>
        PlayLifecycleTransitionCommand.Exit;

    public UcliCode ActionRejectedCode =>
        PlayModeErrorCodes.PlayModeExitRejected;

    public string ActionDisplayName => "Play Mode exit";

    public string CommandDisplayName => "Play Mode exit";

    public string SessionNotAvailableMessage =>
        "Registered GUI daemon session is not available for Play Mode exit.";

    public string RequiresGuiEditorMessage =>
        "Play Mode exit requires a registered GUI daemon session.";

    public UnityRequestPayload CreatePayload (
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? requiredStart)
    {
        return new UnityRequestPayload.PlayExit(
            registration,
            requiredStart);
    }

    public LifecycleExecutionTerminalRecord CreateHostExitTerminalRecord (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalFacts terminalFacts)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new PlayExitLifecycleExecutionTerminalRecord(
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
            is PlayExitLifecycleExecutionTerminalRecord playExitRecord)
        {
            result = playExitRecord.Result;
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
            PlayLifecycleTransitionOutcome.Exited =>
                ValidateExited(transition.Before, currentSnapshot),
            PlayLifecycleTransitionOutcome.AlreadyExited =>
                ValidateAlreadyExited(
                    transition.Before,
                    currentSnapshot),
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
        return outcome is PlayLifecycleTransitionOutcome.Exited
            or PlayLifecycleTransitionOutcome.AlreadyExited;
    }

    public PlayExitExecutionOutput CreateOutput (
        PlayCommandExecutionContext context,
        ITerminalExecutionRef terminalExecutionRef,
        PlayLifecycleSnapshotOutput lifecycle,
        PlayTransitionSuccessOutput transition)
    {
        return new PlayExitExecutionOutput(
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

    private static ApplicationFailure? ValidateExited (
        UnityEditorObservation before,
        UnityEditorObservation after)
    {
        if (!IsEnteredSnapshot(before))
        {
            return CreateStateUnknownFailure(
                "Unity play exit reported exited without a playing before snapshot.");
        }

        if (!IsReadyStoppedSnapshot(after))
        {
            return CreateStateUnknownFailure(
                "Unity play exit reported exited without a ready stopped snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration
            == after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure(
                "Unity play exit reported exited without changing generations.playModeGeneration.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateAlreadyExited (
        UnityEditorObservation before,
        UnityEditorObservation after)
    {
        if (!IsStoppedPlayModeSnapshot(before)
            || !IsStoppedPlayModeSnapshot(after))
        {
            return CreateStateUnknownFailure(
                "Unity play exit reported alreadyExited without a stopped snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration
            != after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure(
                "Unity play exit reported alreadyExited after changing generations.playModeGeneration.");
        }

        return null;
    }

    private static bool IsReadyStoppedSnapshot (
        UnityEditorObservation snapshot)
    {
        return IsStoppedPlayModeSnapshot(snapshot)
            && snapshot.State.LifecycleState
                == UnityEditorLifecycleState.Ready;
    }

    private static bool IsStoppedPlayModeSnapshot (
        UnityEditorObservation snapshot)
    {
        var playMode = snapshot.State.PlayMode;
        return playMode.State == UnityEditorPlayModeState.Stopped
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
