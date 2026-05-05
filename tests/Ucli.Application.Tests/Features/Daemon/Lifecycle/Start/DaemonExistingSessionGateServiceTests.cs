using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonExistingSessionGateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingSucceeds_ReturnsAlreadyRunning ()
    {
        var session = CreateSession(processId: 4001);
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(static _ => ValueTask.CompletedTask),
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
            daemonPingClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new TimeoutException("timeout"))),
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
            daemonPingClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("stale"))),
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
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupRuns_UsesRemainingTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(timeout =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(120));
                return ValueTask.FromException(new InvalidOperationException("stale"));
            }),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-stale-remaining-timeout"),
            CreateSession(processId: 4006),
            TimeSpan.FromMilliseconds(300),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.True(cleanupService.LastStaleCleanupTimeout < TimeSpan.FromMilliseconds(260));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenStaleSessionCleanupCannotStartWithinDeadline_ReturnsTimeoutWithoutCleanup ()
    {
        var timeProvider = new ManualTimeProvider();
        var cleanupService = new StubDaemonSessionCleanupService();
        var service = new DaemonExistingSessionGateService(
            daemonPingClient: new StubDaemonPingClient(timeout =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(80));
                return ValueTask.FromException(new InvalidOperationException("stale"));
            }),
            reachabilityClassifier: new StubDaemonReachabilityClassifier(static _ => true),
            daemonSessionCleanupService: cleanupService,
            timeProvider: timeProvider);

        var result = await service.TryHandleExistingSession(
            CreateContext("fingerprint-existing-stale-timeout-before-cleanup"),
            CreateSession(processId: 4007),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
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
            daemonPingClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("stale"))),
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
            daemonPingClient: new StubDaemonPingClient(static _ => ValueTask.FromException(new InvalidOperationException("unexpected"))),
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
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,

            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<TimeSpan, ValueTask> handler;

        public StubDaemonPingClient (Func<TimeSpan, ValueTask> handler)
        {
            this.handler = handler;
        }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            return handler(timeout);
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

        public TimeSpan LastStaleCleanupTimeout { get; private set; }

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
            LastStaleCleanupTimeout = timeout;
            return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
        }
    }
}
