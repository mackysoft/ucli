using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Tests;

internal static class PlayExitCommandTestData
{
    public static PlayExitExecutionOutput CreateOutput ()
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None, false, false),
            playModeGeneration: 3);
        var transition = new PlayTransitionSuccessOutput(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.Exited,
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current));

        return new PlayExitExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            LifecycleExecutionRef: PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayExit),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: UnityEditorMode.Gui,
            LifecycleState: UnityEditorLifecycleState.Ready,
            BlockingReason: null,
            CompileState: PlayCommandOutputTestData.CompileState,
            Generations: current.State.Generations,
            CanAcceptExecutionRequests: true,
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
            UnityEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Playing,
                UnityEditorPlayModeTransition.None,
                true,
                true),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.Exiting,
                false,
                true),
            playModeGeneration: 3);
        var currentLifecycle =
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current);
        return new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayExit,
                LifecycleExecutionState.Failed),
            applicationState,
            currentLifecycle,
            new PlayTransitionOutput(
                PlayLifecycleTransitionCommand.Exit,
                result,
                PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
                After: null,
                Observed: currentLifecycle,
                ApplicationState: applicationState),
            timeoutMilliseconds: 1000);
    }

    public static PlayTransitionFailureContext CreatePublicationFailureContext ()
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Playing,
                UnityEditorPlayModeTransition.None,
                true,
                true),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None,
                false,
                false),
            playModeGeneration: 3);
        var currentLifecycle =
            PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current);
        return new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateReconnectableExecutionReference(
                LifecycleExecutionKind.PlayExit,
                ExecutionLifecycle.Recovery),
            ExecutionApplicationState.Applied,
            currentLifecycle,
            new PlayTransitionOutput(
                PlayLifecycleTransitionCommand.Exit,
                PlayLifecycleTransitionOutcome.Exited,
                PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
                After: currentLifecycle,
                Observed: null,
                ApplicationState: null),
            timeoutMilliseconds: 1000);
    }

    public static PlayTransitionFailureContext CreateTerminalFailureContext ()
    {
        var output = CreateOutput();
        return new PlayTransitionFailureContext(
            output.Project,
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayExit,
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
