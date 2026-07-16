using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayEnterCommandTestData
{
    public static PlayEnterExecutionOutput CreateOutput (
        IpcPlayTransitionOutcome result = IpcPlayTransitionOutcome.Entered,
        bool includeAfter = true,
        IpcApplicationState applicationState = IpcApplicationState.Indeterminate)
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Stopped, IpcPlayModeTransition.None, false, false),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Playing, IpcPlayModeTransition.None, true, true),
            playModeGeneration: 3);
        var transition = new PlayTransitionOutput(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: result,
            Before: PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
            After: includeAfter ? PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current) : null,
            Observed: includeAfter ? null : PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current),
            ApplicationState: includeAfter ? null : applicationState);

        return new PlayEnterExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: IpcEditorLifecycleState.PlayMode,
            BlockingReason: IpcEditorBlockingReason.PlayMode,
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
}
