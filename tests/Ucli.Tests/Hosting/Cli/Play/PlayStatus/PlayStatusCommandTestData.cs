using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;

namespace MackySoft.Ucli.Tests;

internal static class PlayStatusCommandTestData
{
    public static PlayStatusExecutionOutput CreateOutput ()
    {
        return new PlayStatusExecutionOutput(
            Project: PlayCommandOutputTestData.CreateProject(),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: PlayCommandOutputTestData.ServerVersion,
            EditorMode: "gui",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: PlayCommandOutputTestData.CompileState,
            CompileGeneration: PlayCommandOutputTestData.CompileGeneration,
            DomainReloadGeneration: PlayCommandOutputTestData.DomainReloadGeneration,
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: PlayCommandOutputTestData.ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: PlayCommandOutputTestData.CreatePlayModeOutput(PlayCommandOutputTestData.CreatePlayMode("stopped", "none", false, false, "2")),
            TimeoutMilliseconds: 1000);
    }
}
