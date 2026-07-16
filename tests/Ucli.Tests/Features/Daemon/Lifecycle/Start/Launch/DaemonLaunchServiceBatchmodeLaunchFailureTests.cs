using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

public sealed class DaemonLaunchServiceBatchmodeLaunchFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLaunchFails_RunsCompensationAndReturnsLaunchFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-fail"));
        var timeProvider = new ManualTimeProvider();
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchError = ExecutionError.InternalError("launch failed");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedBeforeProcessIdWasRecorded(
            compensationService,
            launchSessionService,
            context);
        var diagnosis = DaemonDiagnosisStoreAssert.LatestDiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(DaemonDiagnosisReason.StartupFailed, diagnosis.Reason);
        Assert.Equal(launchError.Message, diagnosis.Message);
        Assert.Equal(initialSession.IssuedAtUtc, diagnosis.SessionIssuedAtUtc);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.NotNull(result.Startup);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            context,
            AssertStartupLaunchAttemptId(result.Startup),
            DaemonStartupStatus.Failed,
            DaemonStartupProcessAction.None);
        Assert.Equal(diagnosis, launchAttempt.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCompensationFails_ReturnsInternalError ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-compensation-fail"));
        var timeProvider = new ManualTimeProvider();
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchError = ExecutionError.InternalError("launch failed");
        var cleanupError = ExecutionError.InternalError("cleanup failed");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(cleanupError),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and cleanup failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosisError=", error.Message, StringComparison.Ordinal);
        Assert.Contains("CleanupError=cleanup failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchFailureCompletesAfterDeadline_ReturnsTimeoutAndRunsOwnedCompensation ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-timeout-compensation"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var timeProvider = new ManualTimeProvider();
        var launchError = ExecutionError.InternalError("launch failed after timeout");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            LaunchDelay = TimeSpan.FromMilliseconds(50),
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
            TimeProvider = timeProvider,
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1), timeProvider),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutProcessTarget(
            compensationService,
            context,
            timeout: TimeSpan.FromSeconds(10));
        Assert.Empty(diagnosisStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCancellationRequestedAfterLaunchFailure_RethrowsAfterOwnedCompensation ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-cancel-after-failure"));
        var timeProvider = new ManualTimeProvider();
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchError = ExecutionError.InternalError("launch failed after cancel");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        using var cancellationSource = new CancellationTokenSource();
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            OnLaunch = cancellationSource.Cancel,
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
                    DaemonEditorMode.Batchmode,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    cancellationToken: cancellationSource.Token)
                .AsTask());
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        Assert.True(cancellationSource.IsCancellationRequested);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutProcessTarget(
            compensationService,
            context,
            timeout: TimeSpan.FromSeconds(10));
        Assert.Empty(diagnosisStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDiagnosisWriteFails_ReturnsInternalError ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-launch-diagnosis-fail"));
        var timeProvider = new ManualTimeProvider();
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchError = ExecutionError.InternalError("launch failed");
        var diagnosisError = ExecutionError.InternalError("diagnosis failed");
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(diagnosisError),
        };
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            timeProvider,
            diagnosisStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Daemon launch failed and diagnosis persistence failed.", error.Message, StringComparison.Ordinal);
        Assert.Contains("LaunchError=launch failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("DiagnosisError=diagnosis failed", error.Message, StringComparison.Ordinal);
        DaemonLaunchInvocationAssert.LaunchCompensationAttemptedWithoutProcessTarget(compensationService, context);
        DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, context);
    }
}
