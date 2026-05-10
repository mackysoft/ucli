using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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
        var launchError = ExecutionError.InternalError("launch failed", UcliCoreErrorCodes.CommandNotImplemented);
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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.CommandNotImplemented, error.Code);
        Assert.Contains("diagnosis cleanup failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(launchError.Message, error.Message, StringComparison.Ordinal);
        Assert.Contains(diagnosisDeleteError.Message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFailsAndGuiFreshLaunchFails_PreservesLaunchFailureCode ()
    {
        var context = CreateContext("fingerprint-start-delete-diagnosis-gui");
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis delete failed")),
        };
        var launchError = ExecutionError.Timeout("gui launch failed", ExecutionErrorCodes.IpcTimeout);
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Failure(launchError),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("diagnosis cleanup failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, launchService.CallCount);
        Assert.Equal(DaemonEditorMode.Gui, launchService.LastEditorMode);
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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-path-invalid"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-existing"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        Assert.Equal(0, cleanupService.CleanupInvalidSessionArtifactsCallCount);
        Assert.Equal(0, cleanupService.CleanupStaleSessionArtifactsCallCount);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(existingSession, existingSessionGateService.LastSession);
        Assert.Null(existingSessionGateService.LastEditorMode);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsAlreadyRunning_DoesNotProbeGuiEditor ()
    {
        var existingSession = CreateSession(processId: 2022);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var existingSessionGateService = new StubDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession),
        };
        var guiAttachService = new StubDaemonGuiEditorAttachService
        {
            NextResult = DaemonStartResult.AlreadyRunning(CreateSession(processId: 3030)),
        };
        var launchService = new StubDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-existing-priority"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(0, guiAttachService.CallCount);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionExists_PropagatesRequestedEditorModeToGate ()
    {
        var existingSession = CreateSession(processId: 2021);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(existingSession),
        };
        var existingSessionGateService = new StubDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession),
        };
        var launchService = new StubDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-existing-editor-mode"),
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Batchmode,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonEditorMode.Batchmode, existingSessionGateService.LastEditorMode);
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

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, existingSessionGateService.CallCount);
        Assert.Equal(existingSession, existingSessionGateService.LastSession);
        Assert.Equal(1, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenNoRunningSessionAndGuiAttachSucceeds_ReturnsAlreadyRunningWithoutFreshLaunch ()
    {
        var context = CreateContext("fingerprint-start-gui-attach");
        var guiSession = CreateSession(
            processId: 6060,
            projectFingerprint: context.ProjectFingerprint) with
        {
            EditorMode = DaemonEditorModeValues.Gui,
        };
        var guiAttachService = new StubDaemonGuiEditorAttachService
        {
            NextResult = DaemonStartResult.AlreadyRunning(guiSession),
        };
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 7070, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(guiSession, result.Session);
        Assert.Equal(1, guiAttachService.CallCount);
        Assert.Null(guiAttachService.LastEditorMode);
        Assert.Equal(0, launchService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenGuiAttachReturnsNull_ContinuesToFreshLaunchWithRequestedMode ()
    {
        var context = CreateContext("fingerprint-start-gui-launch");
        var guiAttachService = new StubDaemonGuiEditorAttachService();
        var launchService = new StubDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(CreateSession(processId: 8081, projectFingerprint: context.ProjectFingerprint)),
        };
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(1, guiAttachService.CallCount);
        Assert.Equal(DaemonEditorMode.Gui, guiAttachService.LastEditorMode);
        Assert.Equal(1, launchService.CallCount);
        Assert.Equal(DaemonEditorMode.Gui, launchService.LastEditorMode);
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Terminate, launchService.LastOnStartupBlocked);
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

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-existing-failed"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

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

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-no-session"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(0, existingSessionGateService.CallCount);
        Assert.Equal(1, launchService.CallCount);
        Assert.Equal(DaemonEditorMode.Batchmode, launchService.LastEditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenGuiEditorModeRequiresFreshLaunch_CallsLaunchWithGuiMode ()
    {
        var launchService = new StubDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-gui-launch-mode"),
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Equal(1, launchService.CallCount);
        Assert.Equal(DaemonEditorMode.Gui, launchService.LastEditorMode);
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

        var result = await operation.StartAsync(
            CreateContext("fingerprint-start-lock-timeout"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = CreateContext("fingerprint-start-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = CreateOperation(
            daemonSessionStore: new StubDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(null),
            },
            daemonSessionCleanupService: new StubDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new StubDaemonExistingSessionGateService(),
            daemonLaunchService: new StubDaemonLaunchService
            {
                NextResult = DaemonStartResult.Started(CreateSession(processId: 2026, projectFingerprint: context.ProjectFingerprint)),
            },
            lifecycleLockProvider: lockProvider);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lockRequest = Assert.IsType<ProjectLifecycleLockRequest>(lockProvider.LastRequest);
        Assert.Equal(context.UnityProjectRoot, lockRequest.UnityProjectRoot);
    }

    private static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonLaunchService daemonLaunchService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IDaemonGuiEditorAttachService? daemonGuiEditorAttachService = null,
        IProjectLifecycleLockProvider? lifecycleLockProvider = null)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonDiagnosisStore: daemonDiagnosisStore ?? new StubDaemonDiagnosisStore(),
            daemonSessionStore: daemonSessionStore,
            daemonSessionCleanupService: daemonSessionCleanupService,
            daemonExistingSessionGateService: daemonExistingSessionGateService,
            daemonGuiEditorAttachService: daemonGuiEditorAttachService ?? new StubDaemonGuiEditorAttachService(),
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: ownerProcessId);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public Action? OnRead { get; set; }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            OnRead?.Invoke();
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

    private sealed class StubDaemonSessionCleanupService : IDaemonSessionCleanupService
    {
        public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CleanupInvalidSessionArtifactsCallCount { get; private set; }

        public int CleanupStaleSessionArtifactsCallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSessionReadResult readResult,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CleanupInvalidSessionArtifactsCallCount++;
            return ValueTask.FromResult(CleanupInvalidSessionArtifactsResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
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

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public ValueTask<DaemonStartResult?> TryHandleExistingSessionAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSession = session;
            LastEditorMode = editorMode;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonGuiEditorAttachService : IDaemonGuiEditorAttachService
    {
        public DaemonStartResult? NextResult { get; set; }

        public int CallCount { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public ValueTask<DaemonStartResult?> TryAttachExistingGuiEditorAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEditorMode = editorMode;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonLaunchService : IDaemonLaunchService
    {
        public DaemonStartResult NextResult { get; set; } = DaemonStartResult.Started(CreateSession(processId: 9090));

        public int CallCount { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public DaemonStartupBlockedProcessPolicy LastOnStartupBlocked { get; private set; }

        public ValueTask<DaemonStartResult> LaunchAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode editorMode,
            DaemonStartupBlockedProcessPolicy onStartupBlocked,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEditorMode = editorMode;
            LastOnStartupBlocked = onStartupBlocked;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult DeleteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int DeleteCallCount { get; private set; }

        public string? LastStorageRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
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

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
            DaemonProcessTerminationTarget? target,
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

        public ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}
