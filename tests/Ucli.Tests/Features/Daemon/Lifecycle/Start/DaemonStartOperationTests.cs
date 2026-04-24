using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;

public sealed class DaemonStartOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenWorkflowBegins_DeletesExistingDiagnosisBeforeReadingSession ()
    {
        var context = CreateContext("fingerprint-start-delete-diagnosis");
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
            OnRead = () => Assert.Equal(1, diagnosisStore.DeleteCallCount),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: new StubDaemonLaunchService
            {
                NextResult = DaemonStartResult.Started(CreateSession(processId: 2024, projectFingerprint: context.ProjectFingerprint)),
            },
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, diagnosisStore.DeleteCallCount);
        Assert.Equal(context.RepositoryRoot, diagnosisStore.LastStorageRoot);
        Assert.Equal(context.ProjectFingerprint, diagnosisStore.LastProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFails_ContinuesSessionReadAndLaunch ()
    {
        var context = CreateContext("fingerprint-start-delete-diagnosis-fail");
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis delete failed")),
        };
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 2025, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, diagnosisStore.DeleteCallCount);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFailsAndLaunchFails_ReturnsAugmentedFailure ()
    {
        var context = CreateContext("fingerprint-start-delete-diagnosis-augmented");
        var diagnosisDeleteError = ExecutionError.InternalError("diagnosis delete failed");
        var launchError = ExecutionError.InternalError("launch failed");
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(diagnosisDeleteError),
        };
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Failure(launchError),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("diagnosis cleanup failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(launchError.Message, error.Message, StringComparison.Ordinal);
        Assert.Contains(diagnosisDeleteError.Message, error.Message, StringComparison.Ordinal);
    }

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
        var existingSessionGateService = new StubDaemonExistingSessionGateService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 2222, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, existingSessionGateService.CallCount);
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
        var existingSessionGateService = new StubDaemonExistingSessionGateService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, existingSessionGateService.CallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenLegacyInvalidSessionCannotBeSafelyStopped_ReturnsFailureWithoutLaunch ()
    {
        var context = CreateContext("fingerprint-start-invalid-legacy-live");
        var legacySession = CreateSession(
            processId: 1111,
            projectFingerprint: context.ProjectFingerprint,
            ownerProcessId: null);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            legacySession);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = readResult,
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var cleanupService = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("cannot be safely replaced", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
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
        var existingSessionGateService = new StubDaemonExistingSessionGateService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 3333)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-path-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(0, existingSessionGateService.CallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsAlreadyRunning_ReturnsWithoutLaunch ()
    {
        var existingSession = CreateSession(processId: 2020);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var existingSessionGateService = new StubDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession),
        };
        var launchService = new StubDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-existing"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(existingSession, existingSessionGateService.LastSession);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsNull_ContinuesLaunchFlow ()
    {
        var context = CreateContext("fingerprint-start-stale-cleaned");
        var existingSession = CreateSession(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var existingSessionGateService = new StubDaemonExistingSessionGateService
        {
            NextResult = null,
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 8888, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(existingSession, existingSessionGateService.LastSession);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsFailure_ReturnsFailureWithoutLaunch ()
    {
        var existingSession = CreateSession(processId: 8080);
        var expectedError = ExecutionError.InternalError("probe failed");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var existingSessionGateService = new StubDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.Failure(expectedError),
        };
        var launchService = new StubDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-existing-failed"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionDoesNotExist_CallsLaunchDirectly ()
    {
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var cleanupService = new StubDaemonSessionCleanupService();
        var existingSessionGateService = new StubDaemonExistingSessionGateService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 9001)),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.Start(CreateContext("fingerprint-start-no-session"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(0, existingSessionGateService.CallCount);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var lockProvider = new StubProjectLifecycleLockProvider
        {
            ThrowTimeoutOnAcquire = true,
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore(),
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: new StubDaemonLaunchService(),
            lifecycleLockProvider: lockProvider);

        var result = await operation.Start(
            CreateContext("fingerprint-start-lock-timeout"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonLaunchService daemonLaunchService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IProjectLifecycleLockProvider? lifecycleLockProvider = null)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonDiagnosisStore: daemonDiagnosisStore ?? new StubDaemonDiagnosisStore(),
            daemonSessionStore: daemonSessionStore,
            daemonSessionCleanupService: daemonSessionCleanupService,
            daemonExistingSessionGateService: daemonExistingSessionGateService,
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
        string projectFingerprint = "fingerprint",
        int? ownerProcessId = 9876)
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
            OwnerProcessId: ownerProcessId);
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

        public Action? OnRead { get; set; }

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            OnRead?.Invoke();
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

    private sealed class StubDaemonSessionCleanupService : IDaemonSessionCleanupService
    {
        public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CleanupInvalidSessionArtifactsCallCount { get; private set; }

        public int CleanupStaleSessionArtifactsCallCount { get; private set; }

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
            return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
        }
    }

    private sealed class StubDaemonExistingSessionGateService : IDaemonExistingSessionGateService
    {
        public DaemonStartResult? NextResult { get; set; }

        public int CallCount { get; private set; }

        public DaemonSession? LastSession { get; private set; }

        public ValueTask<DaemonStartResult?> TryHandleExistingSession (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSession = session;
            return ValueTask.FromResult(NextResult);
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

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult DeleteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int DeleteCallCount { get; private set; }

        public string? LastStorageRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            LastStorageRoot = storageRoot;
            LastProjectFingerprint = projectFingerprint;
            return ValueTask.FromResult(DeleteResult);
        }
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
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