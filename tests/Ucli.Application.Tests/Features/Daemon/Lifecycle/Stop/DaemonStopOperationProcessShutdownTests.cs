using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;
using static MackySoft.Ucli.Application.Tests.DaemonCleanupInvocationAssert;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationProcessShutdownTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownFails_AttemptsProcessTerminationAndReturnsFailure ()
    {
        var shutdownError = ExecutionError.InternalError("shutdown failed");
        var session = DaemonSessionTestFactory.Create(processId: 123);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-failure");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
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
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        AssertProcessTerminationAttempted(processTerminationService, 123, session.ProcessStartedAtUtc);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownResultIsNotRunning_EnsuresProcessStoppedAndReturnsStopped ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 456);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-not-running");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.NotRunning(),
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
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertProcessTerminationAttempted(processTerminationService, 456, session.ProcessStartedAtUtc);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProcessIdIsMissing_CleansUpAfterShutdownWithoutProcessTermination ()
    {
        var session = DaemonSessionTestFactory.Create(processId: null);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-pidless");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertSessionArtifactsInvalidatedWithoutProcessTermination(processTerminationService, artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProcessIdIsMissingAndShutdownFails_ReturnsShutdownFailureWithoutCleanup ()
    {
        var shutdownError = ExecutionError.InternalError("shutdown failed");
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-pidless-failure");
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(DaemonSessionTestFactory.Create(processId: null)),
            shutdownClient: new RecordingDaemonShutdownClient
            {
                NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
            },
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        AssertProcessTerminationAndArtifactCleanupSkipped(processTerminationService, artifactCleaner);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenCliOwnedGuiSessionAllowsProcessShutdown_TerminatesProcess ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero);
        var issuedAtUtc = processStartedAtUtc.AddMinutes(2);
        var session = DaemonSessionTestFactory.Create(
            processId: 654,
            ownerKind: "cli",
            canShutdownProcess: true,
            editorMode: "gui",
            issuedAtUtc: issuedAtUtc,
            processStartedAtUtc: processStartedAtUtc);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-cli-gui");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
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
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertProcessTerminationAttempted(processTerminationService, 654, processStartedAtUtc);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }
}
