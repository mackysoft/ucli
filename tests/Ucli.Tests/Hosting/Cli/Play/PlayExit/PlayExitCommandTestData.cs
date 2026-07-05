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
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            PlayCommandOutputTestData.CreatePlayMode("playing", "none", true, true, "2"));
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            PlayCommandOutputTestData.CreatePlayMode("stopped", "none", false, false, "3"));
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
            EditorMode: "gui",
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: null,
            CompileState: PlayCommandOutputTestData.CompileState,
            CompileGeneration: PlayCommandOutputTestData.CompileGeneration,
            DomainReloadGeneration: PlayCommandOutputTestData.DomainReloadGeneration,
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: PlayCommandOutputTestData.ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: PlayCommandOutputTestData.CreatePlayModeOutput(PlayCommandOutputTestData.CreatePlayMode("stopped", "none", false, false, "3")),
            Transition: transition,
            TimeoutMilliseconds: 1000);
    }
}
