using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests;

internal static class PlayStatusCommandTestData
{
    public static PlayStatusExecutionOutput CreateOutput ()
    {
        var playMode = PlayCommandOutputTestData.CreatePlayMode(
            UnityEditorPlayModeState.Stopped,
            UnityEditorPlayModeTransition.None,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
        return new PlayStatusExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: UnityEditorMode.Gui,
            LifecycleState: UnityEditorLifecycleState.Ready,
            BlockingReason: null,
            CompileState: PlayCommandOutputTestData.CompileState,
            Generations: new UnityEditorGenerationSnapshot(
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
