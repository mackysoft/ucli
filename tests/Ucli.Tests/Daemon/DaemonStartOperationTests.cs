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
    public async Task Start_WhenSessionReadReturnsInvalidSession_CallsRecoveryAndLaunch ()
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
        var recoveryService = new StubDaemonStartRecoveryService
        {
            RecoverInvalidSessionResult = DaemonSessionStoreOperationResult.Success(),
        };
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 2222, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(0, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsInvalidSessionAndRecoveryFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-start-invalid-session-failure");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            CreateSession(processId: 1111, projectFingerprint: context.ProjectFingerprint));
        var expectedError = ExecutionError.InternalError("recover failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = readResult,
        };
        var recoveryService = new StubDaemonStartRecoveryService
        {
            RecoverInvalidSessionResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadReturnsNonRecoverableError_ReturnsFailureWithoutRecoveryOrLaunch ()
    {
        var expectedError = ExecutionError.InvalidArgument("path invalid");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Failure(expectedError, DaemonSessionReadFailureKind.PathInvalid),
        };
        var recoveryService = new StubDaemonStartRecoveryService();
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-path-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(0, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(0, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionPingSucceeds_ReturnsAlreadyRunningWithoutRecoveryOrLaunch ()
    {
        var existingSession = CreateSession(processId: 2020);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var recoveryService = new StubDaemonStartRecoveryService();
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 7777)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-existing"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        Assert.Equal(0, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(0, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionIsStale_CallsStaleRecoveryThenLaunch ()
    {
        var context = CreateContext("fingerprint-start-stale");
        var existingSession = CreateSession(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var recoveryService = new StubDaemonStartRecoveryService
        {
            RecoverStaleSessionResult = DaemonSessionStoreOperationResult.Success(),
        };
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 8888, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(0, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(existingSession, recoveryService.LastStaleSession);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionIsStaleAndRecoveryFails_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-start-stale-fail");
        var existingSession = CreateSession(processId: 8080, projectFingerprint: context.ProjectFingerprint);
        var expectedError = ExecutionError.InternalError("stale recover failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var recoveryService = new StubDaemonStartRecoveryService
        {
            RecoverStaleSessionResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 1234, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionPingTimesOut_ReturnsTimeoutFailureWithoutRecoveryOrLaunch ()
    {
        var existingSession = CreateSession(processId: 2020);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var recoveryService = new StubDaemonStartRecoveryService();
        var launchService = new StubDaemonStartLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 7777)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            daemonStartRecoveryService: recoveryService,
            daemonStartLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, recoveryService.RecoverInvalidSessionCallCount);
        Assert.Equal(0, recoveryService.RecoverStaleSessionCallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    private static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonStartRecoveryService daemonStartRecoveryService,
        IDaemonStartLaunchService daemonStartLaunchService)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: new StubDaemonLifecycleLockProvider(),
            daemonSessionStore: daemonSessionStore,
            daemonPingClient: daemonPingClient,
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonStartRecoveryService: daemonStartRecoveryService,
            daemonStartLaunchService: daemonStartLaunchService);
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

    private sealed class StubDaemonStartRecoveryService : IDaemonStartRecoveryService
    {
        public DaemonSessionStoreOperationResult RecoverInvalidSessionResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult RecoverStaleSessionResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int RecoverInvalidSessionCallCount { get; private set; }

        public int RecoverStaleSessionCallCount { get; private set; }

        public DaemonSession? LastStaleSession { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> RecoverInvalidSession (
            ResolvedUnityProjectContext unityProject,
            DaemonSessionReadResult readResult,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            RecoverInvalidSessionCallCount++;
            return ValueTask.FromResult(RecoverInvalidSessionResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> RecoverStaleSession (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            RecoverStaleSessionCallCount++;
            LastStaleSession = session;
            return ValueTask.FromResult(RecoverStaleSessionResult);
        }
    }

    private sealed class StubDaemonStartLaunchService : IDaemonStartLaunchService
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