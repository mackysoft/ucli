using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

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
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(),
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
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(),
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var lockProvider = new StubProjectLifecycleLockProvider
        {
            ThrowTimeoutOnAcquire = true,
        };
        var operation = new DaemonStopOperation(
            lifecycleLockProvider: lockProvider,
            daemonSessionStore: new StubDaemonSessionStore(),
            shutdownClient: new StubDaemonShutdownClient(),
            processTerminationService: new StubDaemonProcessTerminationService(),
            artifactCleaner: new StubDaemonArtifactCleaner());

        var result = await operation.Stop(
            CreateContext("fingerprint-stop-lock-timeout"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProcessTerminationBudgetIsExhausted_StillAttemptsFinalizationWithFallbackTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 789)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            Delay = TimeSpan.FromMilliseconds(80),
            NextResult = DaemonShutdownAttemptResult.Success(),
            TimeProvider = timeProvider,
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
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner,
            timeProvider: timeProvider);

        var result = await operation.Stop(
            CreateContext("fingerprint-stop-timeout-finalization"),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(DaemonTimeouts.StopCompensationTimeout, processTerminationService.LastTimeout);
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
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
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

    private sealed class StubDaemonShutdownClient : IDaemonShutdownClient
    {
        public DaemonShutdownAttemptResult NextResult { get; set; } = DaemonShutdownAttemptResult.Success();

        public TimeSpan Delay { get; set; }

        public ManualTimeProvider? TimeProvider { get; set; }

        public int CallCount { get; private set; }

        public async ValueTask<DaemonShutdownAttemptResult> SendShutdown (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Delay > TimeSpan.Zero)
            {
                if (TimeProvider != null)
                {
                    TimeProvider.Advance(Delay);
                }
                else
                {
                    throw new InvalidOperationException("ManualTimeProvider is required when shutdown Delay is configured.");
                }
            }

            return NextResult;
        }
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public DateTimeOffset? LastExpectedIssuedAtUtc { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = processId;
            LastExpectedIssuedAtUtc = expectedIssuedAtUtc;
            LastTimeout = timeout;
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
