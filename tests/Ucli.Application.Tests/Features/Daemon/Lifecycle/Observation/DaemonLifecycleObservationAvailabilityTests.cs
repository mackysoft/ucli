using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationAvailabilityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsUsableForSession_WhenMatchingRecoveryLeaseOutlivesFreshnessWindow_ReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observedAtUtc = timeProvider.GetUtcNow()
            - DaemonLifecycleObservationTimings.FreshnessWindow
            - TimeSpan.FromSeconds(1);
        var observation = new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Gui,
                lifecycleState: UnityEditorLifecycleState.Recovering,
                compileState: UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(1, 2, 0, 0),
                playMode: new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId!.Value,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                session.SessionGenerationId,
                timeProvider.GetUtcNow() + TimeSpan.FromSeconds(30)));

        var result = DaemonLifecycleObservationAvailability.IsUsableForSession(
            observation,
            session,
            new RecordingDaemonProcessIdentityAssessor(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);

        Assert.True(result);
    }
}
