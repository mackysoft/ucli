using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayExitCommandTestData
{
    public static PlayExitExecutionOutput CreateOutput (
        string result = IpcPlayTransitionResultNames.Exited,
        bool includeAfter = true,
        string applicationState = IpcPlayApplicationStateNames.Indeterminate)
    {
        var before = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.PlayMode,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Playing, IpcPlayModeTransition.None, true, true),
            playModeGeneration: 2);
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(IpcPlayModeState.Stopped, IpcPlayModeTransition.None, false, false),
            playModeGeneration: 3);
        var transition = new PlayExitTransitionOutput(
            Transition: IpcPlayTransitionCommandNames.Exit,
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

        return new PlayExitExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: IpcEditorLifecycleState.Ready,
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
}
