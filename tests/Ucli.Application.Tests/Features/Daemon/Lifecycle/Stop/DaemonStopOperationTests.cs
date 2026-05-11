using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(123, processTerminationService.LastProcessId);
        Assert.Equal(sessionStore.ReadResult.Session!.ProcessStartedAtUtc, processTerminationService.LastProcessStartedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownResultIsNotRunning_EnsuresProcessStoppedAndReturnsStopped ()
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-not-running"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(456, processTerminationService.LastProcessId);
        Assert.Equal(sessionStore.ReadResult.Session!.ProcessStartedAtUtc, processTerminationService.LastProcessStartedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProcessIdIsMissing_CleansUpAfterShutdownWithoutProcessTermination ()
    {
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: null)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
        };
        var processTerminationService = new StubDaemonProcessTerminationService();
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-pidless"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProcessIdIsMissingAndShutdownFails_ReturnsShutdownFailureWithoutCleanup ()
    {
        var shutdownError = ExecutionError.InternalError("shutdown failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: null)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
        };
        var processTerminationService = new StubDaemonProcessTerminationService();
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var operation = new DaemonStopOperation(
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-pidless-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenUserOwnedGuiSessionDoesNotAllowProcessShutdownAndShutdownSucceeds_InvalidatesEndpointOnly ()
    {
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(
                processId: 456,
                ownerKind: DaemonSessionOwnerKindValues.User,
                canShutdownProcess: false,
                editorMode: DaemonEditorModeValues.Gui)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
        };
        var processTerminationService = new StubDaemonProcessTerminationService();
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-disallowed"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenEndpointOnlyGuiShutdownTimesOut_ReturnsFailureWithoutCleanup ()
    {
        var shutdownError = ExecutionError.Timeout("shutdown timed out");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(
                processId: 456,
                ownerKind: DaemonSessionOwnerKindValues.User,
                canShutdownProcess: false,
                editorMode: DaemonEditorModeValues.Gui)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
        };
        var processTerminationService = new StubDaemonProcessTerminationService();
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-endpoint-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenCliOwnedGuiSessionDoesNotAllowProcessShutdownAndEndpointIsNotRunning_CleansUpEndpointOnly ()
    {
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(
                processId: 457,
                ownerKind: DaemonSessionOwnerKindValues.Cli,
                canShutdownProcess: false,
                editorMode: DaemonEditorModeValues.Gui)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.NotRunning(),
        };
        var processTerminationService = new StubDaemonProcessTerminationService();
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-cli-endpoint-only"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenCliOwnedGuiSessionAllowsProcessShutdown_TerminatesProcess ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero);
        var issuedAtUtc = processStartedAtUtc.AddMinutes(2);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(
                processId: 654,
                ownerKind: DaemonSessionOwnerKindValues.Cli,
                canShutdownProcess: true,
                editorMode: DaemonEditorModeValues.Gui,
                issuedAtUtc: issuedAtUtc,
                processStartedAtUtc: processStartedAtUtc)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
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

        var result = await operation.StopAsync(CreateContext("fingerprint-stop-cli-gui"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Equal(1, shutdownClient.CallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(654, processTerminationService.LastProcessId);
        Assert.Equal(processStartedAtUtc, processTerminationService.LastProcessStartedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = CreateContext("fingerprint-stop-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = new DaemonStopOperation(
            lifecycleLockProvider: lockProvider,
            daemonSessionStore: new StubDaemonSessionStore(),
            shutdownClient: new StubDaemonShutdownClient(),
            processTerminationService: new StubDaemonProcessTerminationService(),
            artifactCleaner: new StubDaemonArtifactCleaner());

        var result = await operation.StopAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.NotRunning, result.Status);
        var lockRequest = Assert.IsType<ProjectLifecycleLockRequest>(lockProvider.LastRequest);
        Assert.Equal(context.UnityProjectRoot, lockRequest.UnityProjectRoot);
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

        var result = await operation.StopAsync(
            CreateContext("fingerprint-stop-lock-timeout"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_WhenProcessTerminationBudgetIsExhausted_StillAttemptsFinalizationWithFallbackTimeout (bool endpointAlreadyNotRunning)
    {
        var timeProvider = new ManualTimeProvider();
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 789)),
        };
        var shutdownClient = new StubDaemonShutdownClient
        {
            Delay = TimeSpan.FromMilliseconds(80),
            NextResult = endpointAlreadyNotRunning
                ? DaemonShutdownAttemptResult.NotRunning()
                : DaemonShutdownAttemptResult.Success(),
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

        var result = await operation.StopAsync(
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

    private static DaemonSession CreateSession (
        int? processId,
        string editorMode = DaemonEditorModeValues.Batchmode,
        string ownerKind = DaemonSessionOwnerKindValues.Cli,
        bool canShutdownProcess = true,
        DateTimeOffset? issuedAtUtc = null,
        DateTimeOffset? processStartedAtUtc = null)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: issuedAtUtc ?? DateTimeOffset.UtcNow,
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc ?? (processId is null ? null : DateTimeOffset.UtcNow),
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

    private sealed class StubDaemonShutdownClient : IDaemonShutdownClient
    {
        public DaemonShutdownAttemptResult NextResult { get; set; } = DaemonShutdownAttemptResult.Success();

        public TimeSpan Delay { get; set; }

        public ManualTimeProvider? TimeProvider { get; set; }

        public int CallCount { get; private set; }

        public async ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
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

        public DateTimeOffset? LastProcessStartedAtUtc { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
            DaemonProcessTerminationTarget? target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = target?.ProcessId;
            LastProcessStartedAtUtc = target?.ProcessStartedAtUtc;
            LastTimeout = timeout;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public object NextResult { get; set; } = DaemonArtifactCleanupResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(ToArtifactCleanupResult(NextResult));
        }

        private static DaemonArtifactCleanupResult ToArtifactCleanupResult (object result)
        {
            return result switch
            {
                DaemonArtifactCleanupResult artifactResult => artifactResult,
                DaemonSessionStoreOperationResult sessionResult => sessionResult.IsSuccess
                    ? DaemonArtifactCleanupResult.Success()
                    : DaemonArtifactCleanupResult.Failure(sessionResult.Error!),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported artifact cleanup result."),
            };
        }
    }
}
