namespace MackySoft.Ucli.Tests.Daemon;

using System.IO;
using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-token-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSessionTokenRequired_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2007);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token required", IpcErrorCodes.SessionTokenRequired))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-token-required"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-access-denied"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsAddressNotAvailable_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2010);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.AddressNotAvailable))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsConnectTimeout_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2008);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new IpcConnectTimeoutException("connect timeout"))),
            artifactCleaner: artifactCleaner,
            daemonProcessIdentityAssessor: new StubDaemonProcessIdentityAssessor
            {
                NextAssessment = new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                    session.IssuedAtUtc,
                    null),
            });

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-connect-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenNamedPipeConnectTimeoutAndTrustedSessionProcessIsNotRunning_CompletesCleanup ()
    {
        var session = CreateSession(processId: 2012);
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new IpcConnectTimeoutException("connect timeout"))),
            artifactCleaner: artifactCleaner,
            daemonProcessIdentityAssessor: new StubDaemonProcessIdentityAssessor
            {
                NextAssessment = new DaemonProcessIdentityAssessment(
                    DaemonProcessIdentityAssessmentStatus.NotRunning,
                    null,
                    null),
            });

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-connect-timeout-dead-process"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeReturnsAddressNotAvailable_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.AddressNotAvailable))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-none-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new StubDaemonInvalidSessionCleanupSafetyEvaluator
            {
                RequiresUnsafeSkipResult = false,
            },
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionHasNoParsedMetadataAndProbeReturnsConnectTimeout_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-connect-timeout");
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new IpcConnectTimeoutException("connect timeout"))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionIsUnsafe_ReturnsSkippedWithoutProbing ()
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
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new InvalidDataException("probe should not run"))),
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new StubDaemonInvalidSessionCleanupSafetyEvaluator
            {
                RequiresUnsafeSkipResult = true,
            },
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

        var result = await operation.Cleanup(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UnsafeInvalidSession, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenProbeFailsUnexpectedly_ReturnsFailureWithoutCleanup ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 2011)),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new InvalidDataException("invalid frame"))),
            artifactCleaner: artifactCleaner);

        var result = await operation.Cleanup(CreateContext("fingerprint-cleanup-probe-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon cleanup reachability", error.Message, StringComparison.Ordinal);
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
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
            },
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test")));

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
        IDaemonInvalidSessionCleanupSafetyEvaluator? invalidSessionCleanupSafetyEvaluator = null,
        IDaemonProcessIdentityAssessor? daemonProcessIdentityAssessor = null,
        IIpcEndpointResolver? endpointResolver = null,
        IDaemonCleanupReachabilityProbe? cleanupReachabilityProbe = null)
    {
        var effectivePingClient = daemonPingClient ?? new StubDaemonPingClient(static () => ValueTask.CompletedTask);
        var effectiveReachabilityClassifier = reachabilityClassifier ?? new DaemonReachabilityClassifier();
        _ = endpointResolver;
        return new DaemonCleanupOperation(
            lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonSessionStore ?? new StubDaemonSessionStore(),
            artifactCleaner ?? new StubDaemonArtifactCleaner(),
            invalidSessionCleanupSafetyEvaluator ?? new StubDaemonInvalidSessionCleanupSafetyEvaluator(),
            cleanupReachabilityProbe ?? new DaemonCleanupReachabilityProbe(
                effectivePingClient,
                effectiveReachabilityClassifier),
            daemonProcessIdentityAssessor ?? new StubDaemonProcessIdentityAssessor());
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

        public int CallCount { get; private set; }

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
            CallCount++;
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
        public bool RequiresUnsafeSkipResult { get; set; }

        public bool RequiresUnsafeSkip (
            ResolvedUnityProjectContext unityProject,
            DaemonSession? session)
        {
            return RequiresUnsafeSkipResult;
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        public DaemonProcessIdentityAssessment NextAssessment { get; set; } = new(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            null,
            null);

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset expectedIssuedAtUtc)
        {
            return NextAssessment;
        }

        public DaemonProcessIdentityAssessment AssessProcess (
            System.Diagnostics.Process process,
            int processId,
            DateTimeOffset expectedIssuedAtUtc)
        {
            return NextAssessment;
        }
    }

    private sealed class StubEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }
}