using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Tests;

internal static class PlayEnterCommandTestData
{
    public static PlayEnterExecutionOutput CreateOutput ()
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None, false, false),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
            playModeGeneration: 3);
        var transition = new PlayTransitionSuccessOutput(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Entered,
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current));

        return new PlayEnterExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            LifecycleExecutionRef: PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayEnter),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: UnityEditorMode.Gui,
            LifecycleState: UnityEditorLifecycleState.PlayMode,
            BlockingReason: UnityEditorBlockingReason.PlayMode,
            CompileState: PlayCommandOutputTestData.CompileState,
            Generations: current.State.Generations,
            CanAcceptExecutionRequests: false,
            ObservedAtUtc: PlayCommandOutputTestData.ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: current.State.PlayMode,
            Transition: transition,
            TimeoutMilliseconds: 1000);
    }

    public static PlayTransitionFailureContext CreateFailureContext (
        PlayLifecycleTransitionOutcome result,
        ExecutionApplicationState applicationState)
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None,
                false,
                false),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Playing,
                UnityEditorPlayModeTransition.Entering,
                false,
                true),
            playModeGeneration: 3);
        var currentLifecycle =
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current);
        return new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayEnter,
                LifecycleExecutionState.Failed),
            applicationState,
            currentLifecycle,
            new PlayTransitionOutput(
                PlayLifecycleTransitionCommand.Enter,
                result,
                PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
                After: null,
                Observed: currentLifecycle,
                ApplicationState: applicationState),
            timeoutMilliseconds: 1000);
    }

    public static PlayTransitionFailureContext CreateWaitFailureContext ()
    {
        return new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateReconnectableExecutionReference(
                LifecycleExecutionKind.PlayEnter,
                ExecutionLifecycle.Active),
            ExecutionApplicationState.Unknown);
    }

    public static PlayTransitionFailureContext CreateTerminalFailureContext ()
    {
        var output = CreateOutput();
        return new PlayTransitionFailureContext(
            output.Project,
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayEnter,
                LifecycleExecutionState.Failed),
            ExecutionApplicationState.Applied,
            new PlayLifecycleSnapshotOutput(
                output.ServerVersion,
                output.EditorMode,
                output.Project.UnityVersion,
                output.Project.ProjectFingerprint,
                output.LifecycleState,
                output.BlockingReason,
                output.CompileState,
                output.Generations,
                output.CanAcceptExecutionRequests,
                output.ObservedAtUtc,
                output.ActionRequired,
                output.PrimaryDiagnostic,
                output.PlayMode),
            new PlayTransitionOutput(
                output.Transition.Transition,
                output.Transition.Result,
                output.Transition.Before,
                output.Transition.After,
                Observed: null,
                ApplicationState: null),
            output.TimeoutMilliseconds);
    }
}
