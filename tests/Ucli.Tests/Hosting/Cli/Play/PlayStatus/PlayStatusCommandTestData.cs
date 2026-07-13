using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayStatusCommandTestData
{
    public static PlayStatusExecutionOutput CreateOutput ()
    {
        var playMode = PlayCommandOutputTestData.CreatePlayMode(
            IpcPlayModeState.Stopped,
            IpcPlayModeTransition.None,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
        return new PlayStatusExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: IpcEditorLifecycleState.Ready,
            BlockingReason: null,
            CompileState: PlayCommandOutputTestData.CompileState,
            Generations: new IpcUnityGenerationSnapshot(
                PlayCommandOutputTestData.CompileGeneration,
                PlayCommandOutputTestData.DomainReloadGeneration,
                AssetRefreshGeneration: 0,
                PlayModeGeneration: 2),
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: PlayCommandOutputTestData.ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode,
            TimeoutMilliseconds: 1000);
    }
}
