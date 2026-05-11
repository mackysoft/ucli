using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
namespace MackySoft.Ucli.Tests.Daemon;

using System.IO;
using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.TestDoubles;

public sealed class DaemonCleanupOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeShowsNotRunning_CompletesCleanup ()
    {
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-none"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token invalid", IpcSessionErrorCodes.SessionTokenInvalid))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-none-live"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-running"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token invalid", IpcSessionErrorCodes.SessionTokenInvalid))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-token-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException("token required", IpcSessionErrorCodes.SessionTokenRequired))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-token-required"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            artifactCleaner: artifactCleaner,
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-stale"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-access-denied"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-connect-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenNamedPipeConnectTimeoutAndTrustedSessionProcessIsNotRunning_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = CreateSession(processId: 2012);
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new IpcConnectTimeoutException("connect timeout"))),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-connect-timeout-dead-process"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
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

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-none-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            NextResult = DaemonArtifactCleanupResult.Success(),
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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            NextResult = DaemonArtifactCleanupResult.Success(),
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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-probe-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            endpointResolver: new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test")));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(DaemonCleanupSkipReason.UncertainReachability, result.SkipReason);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = CreateContext("fingerprint-cleanup-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = CreateOperation(
            lifecycleLockProvider: lockProvider,
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))));

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lockRequest = Assert.IsType<ProjectLifecycleLockRequest>(lockProvider.LastRequest);
        Assert.Equal(context.UnityProjectRoot, lockRequest.UnityProjectRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var operation = CreateOperation(
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(throwTimeout: true));

        var result = await operation.CleanupAsync(CreateContext("fingerprint-cleanup-lock-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
        IDaemonArtifactCleaner? artifactCleaner = null,
        IDaemonInvalidSessionCleanupSafetyEvaluator? invalidSessionCleanupSafetyEvaluator = null,
        IIpcEndpointResolver? endpointResolver = null,
        IDaemonCleanupReachabilityProbe? cleanupReachabilityProbe = null)
    {
        var effectivePingClient = daemonPingClient ?? new StubDaemonPingClient(static () => ValueTask.CompletedTask);
        _ = endpointResolver;
        return new DaemonCleanupOperation(
            lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonSessionStore ?? new StubDaemonSessionStore(),
            artifactCleaner ?? new StubDaemonArtifactCleaner(),
            invalidSessionCleanupSafetyEvaluator ?? new StubDaemonInvalidSessionCleanupSafetyEvaluator(),
            cleanupReachabilityProbe ?? new DaemonCleanupReachabilityProbe(effectivePingClient));
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
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

        public ValueTask PingAsync (
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
        public DaemonArtifactCleanupResult NextResult { get; set; } = DaemonArtifactCleanupResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
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
