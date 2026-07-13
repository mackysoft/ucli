using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleDerivedValues_WhenLifecycleStateChanges_FollowCurrentState ()
    {
        var ready = CreateObservation(IpcEditorLifecycleState.Ready);
        var recovering = ready with
        {
            LifecycleState = IpcEditorLifecycleState.Recovering,
        };

        Assert.Null(ready.BlockingReason);
        Assert.True(ready.CanAcceptExecutionRequests);
        Assert.Equal(IpcEditorBlockingReason.Recovery, recovering.BlockingReason);
        Assert.False(recovering.CanAcceptExecutionRequests);
    }

    private static DaemonLifecycleObservation CreateObservation (IpcEditorLifecycleState lifecycleState)
    {
        return new DaemonLifecycleObservation(
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UnixEpoch,
            EditorMode: "gui",
            LifecycleState: lifecycleState,
            CompileState: IpcCompileState.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            ActionRequired: null,
            PrimaryDiagnostic: null);
    }
}
