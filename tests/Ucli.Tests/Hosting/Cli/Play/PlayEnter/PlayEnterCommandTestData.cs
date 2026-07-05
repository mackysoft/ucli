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
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            PlayCommandOutputTestData.CreatePlayMode("stopped", "none", false, false, "2"));
        var current = PlayCommandOutputTestData.CreateLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            PlayCommandOutputTestData.CreatePlayMode("playing", "none", true, true, "3"));
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
            EditorMode: "gui",
            LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
            CompileState: PlayCommandOutputTestData.CompileState,
            CompileGeneration: PlayCommandOutputTestData.CompileGeneration,
            DomainReloadGeneration: PlayCommandOutputTestData.DomainReloadGeneration,
            CanAcceptExecutionRequests: false,
            ObservedAtUtc: PlayCommandOutputTestData.ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: PlayCommandOutputTestData.CreatePlayModeOutput(PlayCommandOutputTestData.CreatePlayMode("playing", "none", true, true, "3")),
            Transition: transition,
            TimeoutMilliseconds: 1000);
    }
}
