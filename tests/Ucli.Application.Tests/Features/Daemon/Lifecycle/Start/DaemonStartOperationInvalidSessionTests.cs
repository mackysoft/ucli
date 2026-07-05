using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationInvalidSessionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsInvalidSession_CleansArtifactsThenStartsFreshDaemon ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-invalid-session");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            DaemonSessionTestFactory.Create(processId: 1111, projectFingerprint: context.ProjectFingerprint));
        var sessionStore = new RecordingDaemonSessionStore(readResult);
        var cleanupService = new RecordingDaemonSessionCleanupService
        {
            CleanupInvalidSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService();
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 2222, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        DaemonStartOperationInvocationAssert.InvalidSessionCleanupAttemptedBeforeFreshLaunch(
            cleanupService,
            existingSessionGateService,
            launchService,
            context,
            readResult);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsInvalidSessionAndCleanupFails_ReturnsFailureWithoutLaunch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-invalid-session-failure");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            DaemonSessionTestFactory.Create(processId: 1111, projectFingerprint: context.ProjectFingerprint));
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var sessionStore = new RecordingDaemonSessionStore(readResult);
        var cleanupService = new RecordingDaemonSessionCleanupService
        {
            CleanupInvalidSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService();
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        DaemonStartOperationInvocationAssert.InvalidSessionCleanupFailureStoppedBeforeGateOrLaunch(
            cleanupService,
            existingSessionGateService,
            launchService,
            context,
            readResult);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenLegacyInvalidSessionCannotBeSafelyStopped_ReturnsFailureWithoutLaunch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-invalid-legacy-live");
        var legacySession = DaemonSessionTestFactory.Create(
            processId: 1111,
            projectFingerprint: context.ProjectFingerprint,
            ownerProcessId: null);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            legacySession);
        var sessionStore = new RecordingDaemonSessionStore(readResult);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var cleanupService = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("cannot be safely replaced", error.Message, StringComparison.Ordinal);
        DaemonStartOperationInvocationAssert.UnsafeLegacyInvalidSessionCleanupSkippedBeforeLaunch(
            processTerminationService,
            artifactCleaner,
            launchService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsNonInvalidSessionError_ReturnsFailureWithoutCleanupOrLaunch ()
    {
        var expectedError = ExecutionError.InvalidArgument("path invalid");
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Failure(expectedError, DaemonSessionReadFailureKind.PathInvalid));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService();
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 3333)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-path-invalid"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        DaemonStartOperationInvocationAssert.SessionReadFailureStoppedBeforeRecoveryOrLaunch(
            cleanupService,
            existingSessionGateService,
            launchService);
    }
}
