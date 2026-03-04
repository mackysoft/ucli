namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStartOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsInvalidSession_CallsCleanupAndLaunch ()
    {
        var context = CreateContext("fingerprint-start-invalid-session");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            CreateSession(processId: 1111, projectFingerprint: context.ProjectFingerprint));
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = readResult,
        };
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupInvalidSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 2222, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsInvalidSessionAndCleanupFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-start-invalid-session-failure");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            CreateSession(processId: 1111, projectFingerprint: context.ProjectFingerprint));
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = readResult,
        };
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupInvalidSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsNonInvalidSessionError_ReturnsFailureWithoutCleanupOrLaunch ()
    {
        var expectedError = ExecutionError.InvalidArgument("path invalid");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Failure(expectedError, DaemonSessionReadFailureKind.PathInvalid),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-path-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionPingSucceeds_ReturnsAlreadyRunningWithoutCleanupOrLaunch ()
    {
        var existingSession = CreateSession(processId: 2020);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 7777)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-existing"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionIsStale_CallsStaleCleanupThenLaunch ()
    {
        var context = CreateContext("fingerprint-start-stale");
        var existingSession = CreateSession(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Success(),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 8888, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(existingSession, cleanupService.LastStaleSession);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionIsStaleAndCleanupFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-start-stale-fail");
        var existingSession = CreateSession(processId: 8080, projectFingerprint: context.ProjectFingerprint);
        var expectedError = ExecutionError.InternalError("stale cleanup failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService
        {
            CleanupStaleSessionArtifactsResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 1234, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionPingTimesOut_ReturnsTimeoutFailureWithoutCleanupOrLaunch ()
    {
        var existingSession = CreateSession(processId: 2020);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 7777)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            daemonSessionCleanupService: cleanupService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    private static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonLaunchService daemonLaunchService)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: new StubDaemonLifecycleLockProvider(),
            daemonSessionStore: daemonSessionStore,
            daemonPingClient: daemonPingClient,
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionCleanupService: daemonSessionCleanupService,
            daemonLaunchService: daemonLaunchService);
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

    private sealed class StubDaemonLifecycleLockProvider : IDaemonLifecycleLockProvider
    {
        public ValueTask<IAsyncDisposable> Acquire (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IAsyncDisposable>(new NoopAsyncDisposable());
        }

        private sealed class NoopAsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync ()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
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

    private sealed class StubDaemonSessionCleanupService : IDaemonSessionCleanupService
    {
        public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CleanupInvalidSessionArtifactsCallCount { get; private set; }

        public int CleanupStaleSessionArtifactsCallCount { get; private set; }

        public DaemonSession? LastStaleSession { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifacts (
            ResolvedUnityProjectContext unityProject,
            DaemonSessionReadResult readResult,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CleanupInvalidSessionArtifactsCallCount++;
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

    private sealed class StubDaemonLaunchService : IDaemonLaunchService
    {
        public DaemonStartResult NextResult { get; set; } = DaemonStartResult.Started(CreateSession(processId: 9090));

        public int CallCount { get; private set; }

        public ValueTask<DaemonStartResult> Launch (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}