namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonExistingSessionGateServiceRecoveryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOutDuringRecovery_WaitsForSameProcessAndReturnsAlreadyRunning ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-timeout-recovery");
        var session = DaemonSessionTestFactory.Create(processId: 4009, projectFingerprint: context.ProjectFingerprint) with
        {
            EditorInstanceId = "editor-instance-recovering",
        };
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("recovering"),
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateRecoveringObservation(session)),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                session.ProcessStartedAtUtc,
                Error: null),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        DaemonExistingSessionGateServiceAssert.RecoveringSessionReturnedAlreadyRunningWithoutStaleCleanup(
            result,
            session,
            cleanupService);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringProbeBudgetExpires_ReturnsNullForStartFlow ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-recovery-handoff");
        var session = DaemonSessionTestFactory.Create(processId: 4010, projectFingerprint: context.ProjectFingerprint) with
        {
            EditorInstanceId = "editor-instance-recovery-handoff",
        };
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("initial endpoint timeout"),
            new TimeoutException("recovering endpoint timeout"))
        {
            OnPingAndRead = () => timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap + TimeSpan.FromMilliseconds(1)),
        };
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateRecoveringObservation(session)),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                session.ProcessStartedAtUtc,
                Error: null),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient,
            cleanupService: cleanupService,
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: processIdentityAssessor,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromSeconds(5),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        DaemonPingInfoClientAssert.ExistingSessionPingAttempted(
            pingClient,
            context,
            session,
            expectedCount: 2,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRecoveringSidecarLacksEditorInstanceId_ReturnsTimeoutFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-timeout-recovery-missing-editor-instance");
        var session = DaemonSessionTestFactory.Create(processId: 4011, projectFingerprint: context.ProjectFingerprint);
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                DaemonExistingSessionGateServiceTestSupport.CreateRecoveringObservation(session)),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("recovering")),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: new RecordingDaemonProcessIdentityAssessor
            {
                Assessment = new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                    session.ProcessStartedAtUtc,
                    Error: null),
            });

        var result = await service.TryHandleExistingSessionAsync(
            context,
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }
}
