using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupObserverFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCanceledByWaitingProgressAfterGuiProcessStarts_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-gui-wait-progress-cancel");
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 1, TimeSpan.Zero);
        const int processId = 7641;
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) =>
            {
                if (progressEvent != DaemonStartProgressEvent.WaitingForEndpoint)
                {
                    return ValueTask.CompletedTask;
                }

                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled(cancellationTokenSource.Token);
            },
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver();
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationTokenSource.Token)
                .AsTask());

        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId,
            processStartedAtUtc);
        Assert.Empty(guiStartupObserver.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEndpointReadyProgressFailsAfterGuiProcessStarts_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-gui-endpoint-progress-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 2, TimeSpan.Zero);
        const int processId = 7642;
        var registeredSession = DaemonSessionTestFactory.Create(
            processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress) with
        {
            EditorMode = ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            ProcessStartedAtUtc = processStartedAtUtc,
        };
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Success(registeredSession),
        };
        var progressFailure = new InvalidOperationException("endpoint-ready progress failed");
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) => progressEvent == DaemonStartProgressEvent.EndpointRegistered
                ? ValueTask.FromException(progressFailure)
                : ValueTask.CompletedTask,
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver);

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationToken: CancellationToken.None)
                .AsTask());

        Assert.Same(progressFailure, actualFailure);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId,
            processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenProgressFailureCompensationThrows_PreservesProgressFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-gui-progress-cleanup-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 3, TimeSpan.Zero);
        const int processId = 7643;
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
        };
        var progressFailure = new InvalidOperationException("waiting progress failed");
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) => progressEvent == DaemonStartProgressEvent.WaitingForEndpoint
                ? ValueTask.FromException(progressFailure)
                : ValueTask.CompletedTask,
        };
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            Handler = (_, _, _, _, _) => ValueTask.FromException<DaemonSessionStoreOperationResult>(
                new IOException("launch compensation failed")),
        };
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher);

        var actualFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationToken: CancellationToken.None)
                .AsTask());

        Assert.Same(progressFailure, actualFailure);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId,
            processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupWaitIsCanceled_RunsCompensationAndRethrows ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-launch-cancel");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7654, processStartedAtUtc),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            Handler = _ =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled<DaemonGuiStartupObservationResult>(cancellationTokenSource.Token);
            },
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    TimeSpan.FromMilliseconds(500),
                    DaemonEditorMode.Gui,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    cancellationToken: cancellationTokenSource.Token)
                .AsTask());

        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 7654,
            processStartedAtUtc: processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCanceledCleanupIgnoresDeadline_RethrowsAndBlocksSuccessorLaunchUntilCleanupQuiesces ()
    {
        var timeProvider = new ManualTimeProvider();
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-gui-launch-owned-cancel");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 2, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7655, processStartedAtUtc),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            Handler = _ =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled<DaemonGuiStartupObservationResult>(cancellationTokenSource.Token);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource<DaemonSessionStoreOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            Handler = (_, _, _, _, _) =>
            {
                cleanupStarted.TrySetResult();
                return new ValueTask<DaemonSessionStoreOperationResult>(releaseCleanup.Task);
            },
        };
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var canceledLaunchTask = service.LaunchAsync(
                context,
                TimeSpan.FromMilliseconds(500),
                DaemonEditorMode.Gui,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: cancellationTokenSource.Token)
            .AsTask();
        await TestAwaiter.WaitAsync(
            cleanupStarted.Task,
            "Canceled launch cleanup start",
            TimeSpan.FromSeconds(5));

        timeProvider.Advance(DaemonTimeouts.LaunchCompensationTimeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestAwaiter.WaitAsync(
            canceledLaunchTask,
            "Canceled launch rethrow after compensation deadline",
            TimeSpan.FromSeconds(5)));

        var successorLaunchTask = service.LaunchAsync(
                context,
                TimeSpan.FromMilliseconds(100),
                DaemonEditorMode.Gui,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var successorResult = await TestAwaiter.WaitAsync(
            successorLaunchTask,
            "Successor launch compensation admission",
            TimeSpan.FromSeconds(5));
        Assert.Equal(DaemonStartStatus.Failed, successorResult.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, successorResult.Error!.Kind);
        Assert.Single(guiLauncher.Invocations);

        releaseCleanup.TrySetResult(DaemonSessionStoreOperationResult.Success());
        var quiescenceError = await TestAwaiter.WaitAsync(
            compensationOperationOwner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for launch compensation cleanup.")
                .AsTask(),
            "Launch compensation quiescence",
            TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenEditorModeGuiStartupObserverFails_RunsCompensationAndReturnsFailure ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-launch-observer-fail");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var startupError = ExecutionError.InternalError("observer failed");
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(8765, processStartedAtUtc),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Failure(startupError),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(startupError, result.Error);
        DaemonLaunchInvocationAssert.LaunchCompensationAttempted(
            compensationService,
            context,
            processId: 8765,
            processStartedAtUtc: processStartedAtUtc);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), result.Startup!.ProcessAction);
        var launchAttempt = DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed), launchAttempt.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated), launchAttempt.ProcessAction);
    }
}
