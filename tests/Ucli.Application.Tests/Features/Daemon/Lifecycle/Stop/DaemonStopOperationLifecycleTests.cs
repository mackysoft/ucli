using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;
using static MackySoft.Ucli.Application.Tests.DaemonCleanupInvocationAssert;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationLifecycleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = CreateOperation(
            lifecycleLockProvider: lockProvider,
            sessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)));

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.NotRunning, result.Status);
        ProjectLifecycleLockProviderAssert.LifecycleLockAcquiredFor(lockProvider, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var lockProvider = new StubProjectLifecycleLockProvider
        {
            ThrowTimeoutOnAcquire = true,
        };
        var operation = CreateOperation(
            lifecycleLockProvider: lockProvider,
            sessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)));

        var result = await operation.StopAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-lock-timeout"),
            DefaultTimeout,
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_WhenProcessTerminationBudgetIsExhausted_StillAttemptsFinalizationWithFallbackTimeout (bool endpointAlreadyNotRunning)
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.Create(processId: 789);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-timeout-finalization");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            Delay = TimeSpan.FromMilliseconds(80),
            NextResult = endpointAlreadyNotRunning
                ? DaemonShutdownAttemptResult.NotRunning()
                : DaemonShutdownAttemptResult.Success(),
            TimeProvider = timeProvider,
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner,
            timeProvider: timeProvider);

        var result = await operation.StopAsync(
            context,
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertProcessTerminationAttempted(
            processTerminationService,
            789,
            session.ProcessStartedAtUtc,
            DaemonTimeouts.StopCompensationTimeout);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }
}
