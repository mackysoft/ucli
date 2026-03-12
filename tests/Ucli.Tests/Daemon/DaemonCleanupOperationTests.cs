namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonCleanupOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeShowsNotRunning_CompletesCleanup ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-none"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeFindsLiveDaemon_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token invalid", IpcErrorCodes.SessionTokenInvalid))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-none-live"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingSucceeds_ReturnsSkippedRunningWithoutCleanup ()
    {
        var session = CreateSession(processId: 2001);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-running"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.Running, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSessionTokenInvalid_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2006);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token invalid", IpcErrorCodes.SessionTokenInvalid))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-token-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsNotRunningException_CompletesCleanup ()
    {
        var session = CreateSession(processId: 2002);
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-stale"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSocketAccessDenied_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2007);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.AccessDenied))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-access-denied"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionCanBeCleaned_CompletesCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-safe");
        var invalidSession = CreateSession(processId: 2003) with
        {
            OwnerProcessId = null,
            ProjectFingerprint = context.ProjectFingerprint,
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    invalidSession),
            },
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new StubDaemonInvalidSessionCleanupSafetyEvaluator
            {
                CanCleanupResult = true,
            });

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionHasNoParsedMetadataAndProbeShowsNotRunning_CompletesCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-null");
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionIsUnsafe_ReturnsSkippedWithoutCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-unsafe");
        var invalidSession = CreateSession(processId: 2004) with
        {
            OwnerProcessId = null,
            ProjectFingerprint = context.ProjectFingerprint,
        };
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    invalidSession),
            },
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new StubDaemonInvalidSessionCleanupSafetyEvaluator
            {
                CanCleanupResult = false,
            });

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UnsafeInvalidSession, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenPingTimesOut_ReturnsSkippedUncertainReachability ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 2005)),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var operation = CreateOperation(
            lifecycleLockProvider: new StubProjectLifecycleLockProvider
            {
                ThrowTimeoutOnAcquire = true,
            });

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-lock-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DaemonCleanupOperation CreateOperation (
        IProjectLifecycleLockProvider? lifecycleLockProvider = null,
        IDaemonSessionStore? daemonSessionStore = null,
        IDaemonPingClient? daemonPingClient = null,
        IDaemonReachabilityClassifier? reachabilityClassifier = null,
        IDaemonArtifactCleaner? artifactCleaner = null,
        IDaemonInvalidSessionCleanupSafetyEvaluator? invalidSessionCleanupSafetyEvaluator = null)
    {
        return new DaemonCleanupOperation(
            lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonSessionStore ?? new StubDaemonSessionStore(),
            daemonPingClient ?? new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier ?? new DaemonReachabilityClassifier(),
            artifactCleaner ?? new StubDaemonArtifactCleaner(),
            invalidSessionCleanupSafetyEvaluator ?? new StubDaemonInvalidSessionCleanupSafetyEvaluator());
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (int? processId)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-endpoint",
            ProcessId: processId,
            OwnerProcessId: 9876);
    }

    private sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
    {
        public bool ThrowTimeoutOnAcquire { get; set; }

        public ValueTask<IAsyncDisposable> Acquire (
            string storageRoot,
            string projectFingerprint,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (ThrowTimeoutOnAcquire)
            {
                throw new TimeoutException("lock timeout");
            }

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

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonInvalidSessionCleanupSafetyEvaluator : IDaemonInvalidSessionCleanupSafetyEvaluator
    {
        public bool CanCleanupResult { get; set; } = true;

        public bool CanCleanup (
            ResolvedUnityProjectContext unityProject,
            DaemonSession? session)
        {
            return CanCleanupResult;
        }
    }
}