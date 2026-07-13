namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonExistingSessionGateServiceStaleCleanupTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupSucceeds_ReturnsNullForFreshLaunch ()
    {
        var cleanupService = new RecordingDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var session = DaemonSessionTestFactory.Create(processId: 4003);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new InvalidOperationException("stale")),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            cleanupService: cleanupService);

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-stale")),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        DaemonStartOperationInvocationAssert.StaleSessionCleanupAttemptedFor(cleanupService, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupRuns_UsesRemainingTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new RecordingDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new InvalidOperationException("stale"))
            {
                OnPingAndRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(120)),
            },
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            cleanupService: cleanupService,
            timeProvider: timeProvider);

        var session = DaemonSessionTestFactory.Create(processId: 4006);
        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-stale-remaining-timeout")),
            session,
            TimeSpan.FromMilliseconds(300),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        DaemonStartOperationInvocationAssert.StaleSessionCleanupAttemptedWithTimeoutLessThan(
            cleanupService,
            session,
            TimeSpan.FromMilliseconds(260));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupCannotStartWithinDeadline_ReturnsTimeoutWithoutCleanup ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new InvalidOperationException("stale"))
            {
                OnPingAndRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(80)),
            },
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            cleanupService: cleanupService,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-stale-timeout-before-cleanup")),
            DaemonSessionTestFactory.Create(processId: 4007),
            TimeSpan.FromMilliseconds(20),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        DaemonExistingSessionGateServiceAssert.CleanupDeadlineFailureReturnedWithoutStaleCleanup(
            result,
            cleanupService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var cleanupService = new RecordingDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new InvalidOperationException("stale")),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            cleanupService: cleanupService);

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-stale-failed")),
            DaemonSessionTestFactory.Create(processId: 4004),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        Assert.Equal(expectedError, result.Error);
    }
}
