namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStopOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownFails_AttemptsProcessTerminationAndReturnsFailure ()
    {
        var shutdownError = ExecutionError.InternalError("shutdown failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 123)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = new DaemonStopOperation(
            lifecycleLockProvider: new StubDaemonLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.Stop(CreateContext("fingerprint-stop-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(123, processTerminationService.LastProcessId);
        Assert.Equal(sessionStore.ReadResult.Session!.IssuedAtUtc, processTerminationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownResultIsNotRunning_SkipsProcessTerminationAndReturnsStopped ()
    {
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 456)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.NotRunning(),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError("must not be called")),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = new DaemonStopOperation(
            lifecycleLockProvider: new StubDaemonLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.Stop(CreateContext("fingerprint-stop-not-running"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
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

    private sealed class StubDaemonShutdownClient : IDaemonShutdownClient
    {
        public DaemonShutdownAttemptResult NextResult { get; set; } = DaemonShutdownAttemptResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonShutdownAttemptResult> SendShutdown (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public DateTimeOffset? LastExpectedIssuedAtUtc { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = processId;
            LastExpectedIssuedAtUtc = expectedIssuedAtUtc;
            return ValueTask.FromResult(NextResult);
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
}