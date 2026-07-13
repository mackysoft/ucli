using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayEnterCommandTestData
{
    public static PlayEnterExecutionOutput CreateOutput (
        string result = IpcPlayTransitionResultNames.Entered,
        bool includeAfter = true,
        string applicationState = IpcPlayApplicationStateNames.Indeterminate)
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Stopped, IpcPlayModeTransition.None, false, false),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Playing, IpcPlayModeTransition.None, true, true),
            playModeGeneration: 3);
        var transition = new PlayEnterTransitionOutput(
            Transition: IpcPlayTransitionCommandNames.Enter,
            Result: result,
            Before: PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(before),
            After: null,
            Observed: null,
            ApplicationState: null);

        if (includeAfter)
        {
            transition = transition with
            {
                After = PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current),
            };
        }
        else
        {
            transition = transition with
            {
                Observed = PlayCommandOutputTestData.CreateLifecycleSnapshotOutput(current),
                ApplicationState = applicationState,
            };
        }

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
