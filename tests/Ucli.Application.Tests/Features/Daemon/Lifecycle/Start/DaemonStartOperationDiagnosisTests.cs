using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationDiagnosisTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenWorkflowBegins_DeletesExistingDiagnosisForUnityProject ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-start-delete-diagnosis"));
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: new RecordingDaemonLaunchService
            {
                NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 2024, projectFingerprint: context.ProjectFingerprint), UnityEditorObservationTestFactory.Create()),
            },
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.StartAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new FakeTimeProvider(DateTimeOffset.UnixEpoch)),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonDiagnosisStoreAssert.DeleteAttemptedFor(diagnosisStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFails_ContinuesSessionReadAndLaunch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-start-delete-diagnosis-fail"));
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis delete failed")),
        };
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 2025, projectFingerprint: context.ProjectFingerprint), UnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.StartAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new FakeTimeProvider(DateTimeOffset.UnixEpoch)),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFailsAndLaunchFails_ReturnsAugmentedFailure ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-start-delete-diagnosis-augmented"));
        var diagnosisDeleteError = ExecutionError.InternalError("diagnosis delete failed");
        var launchError = ExecutionError.InternalError("launch failed", UcliCoreErrorCodes.CommandNotImplemented);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(diagnosisDeleteError),
        };
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Failure(launchError),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.StartAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new FakeTimeProvider(DateTimeOffset.UnixEpoch)),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.CommandNotImplemented, error.Code);
        Assert.Contains(launchError.Message, error.Message, StringComparison.Ordinal);
        Assert.Contains(diagnosisDeleteError.Message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteFailsAndGuiFreshLaunchFails_PreservesLaunchFailureCode ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-start-delete-diagnosis-gui"));
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            DeleteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("diagnosis delete failed")),
        };
        var launchError = ExecutionError.Timeout("gui launch failed", ExecutionErrorCodes.IpcTimeout);
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Failure(launchError),
        };
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing()),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonDiagnosisStore: diagnosisStore);

        var result = await operation.StartAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new FakeTimeProvider(DateTimeOffset.UnixEpoch)),
            editorMode: UnityEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        DaemonStartOperationInvocationAssert.FreshLaunchAttempted(launchService, context, expectedEditorMode: UnityEditorMode.Gui);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDiagnosisDeleteIgnoresDeadline_ContinuesLifecycleOnIndependentLane ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var lifecycleLease = new RecordingAsyncDisposable();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-start-owned-diagnosis-delete"));
        var deleteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelete = new TaskCompletionSource<DaemonDiagnosisStoreOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            DeleteAsyncHandler = (_, _, _) =>
            {
                deleteStarted.TrySetResult();
                return new ValueTask<DaemonDiagnosisStoreOperationResult>(releaseDelete.Task);
            },
        };
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: new RecordingDaemonLaunchService(),
            daemonDiagnosisStore: diagnosisStore,
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(
                (_, _, _) => lifecycleLease),
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var startTask = operation.StartAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await deleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(DaemonTimeouts.SupplementalPersistenceTimeout);
        var result = await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Equal(1, lifecycleLease.DisposeCount);

        var lifecycleMutationResult = await compensationOperationOwner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out before lifecycle mutation began.",
                "Timed out while lifecycle mutation was running.",
                (_, _) => ValueTask.FromResult(true));
        Assert.True(lifecycleMutationResult.IsSuccess);
        Assert.True(lifecycleMutationResult.Value);

        releaseDelete.TrySetResult(DaemonDiagnosisStoreOperationResult.Success());
        var supplementalMutationResult = await compensationOperationOwner.ExecuteAsync(
                    context,
                    DaemonOperationLane.SupplementalPersistence,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for diagnosis cleanup quiescence.",
                    "Timed out while replacement supplemental mutation was running.",
                    (_, _) => ValueTask.FromResult(true))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(supplementalMutationResult.IsSuccess);
    }

    private sealed class RecordingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
