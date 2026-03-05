namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonExistingSessionGateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingSucceeds_ReturnsAlreadyRunning ()
    {
        var session = CreateSession(processId: 4001);
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService());

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-running"),
            session,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOut_ReturnsTimeoutFailure ()
    {
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("timeout"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService());

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-timeout"),
            CreateSession(processId: 4002),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupSucceeds_ReturnsNullForFreshLaunch ()
    {
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var session = CreateSession(processId: 4003);
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new InvalidOperationException("stale"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService);

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-stale"),
            session,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(session, cleanupService.LastStaleSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenSessionIsStaleAndCleanupFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new InvalidOperationException("stale"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService);

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-stale-failed"),
            CreateSession(processId: 4004),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingThrowsUnexpectedError_ReturnsInternalFailure ()
    {
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new InvalidOperationException("unexpected"))),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService());

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-unexpected"),
            CreateSession(processId: 4005),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test-endpoint",
            ProcessId: processId);
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ValueTask> handler;

        public StubDaemonPingClient (Func<ValueTask> handler)
        {
            this.handler = handler;
        }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            return handler();
        }
    }

    private sealed class StubDaemonReachabilityClassifier : IDaemonReachabilityClassifier
    {
        private readonly Func<Exception, bool> predicate;

        public StubDaemonReachabilityClassifier (Func<Exception, bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool IsNotRunning (Exception exception)
        {
            return predicate(exception);
        }
    }

    private sealed class StubDaemonSessionCleanupService : IDaemonSessionCleanupService
    {
        public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CleanupStaleSessionArtifactsCallCount { get; private set; }

        public DaemonSession? LastStaleSession { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifacts (
            ResolvedUnityProjectContext unityProject,
            DaemonSessionReadResult readResult,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CleanupInvalidSessionArtifactsResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifacts (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CleanupStaleSessionArtifactsCallCount++;
            LastStaleSession = session;
            return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
        }
    }
}