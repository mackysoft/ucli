using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceDeadlineTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionInitializationIgnoresCancellation_ReturnsAtDeadlineAndCleansLateSession ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 07, 14, 0, 0, 1, TimeSpan.Zero));
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-late-session-initialization"));
        var initializedSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var initializationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialization = new TaskCompletionSource<DaemonLaunchSessionWriteResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeHandler = (_, _, _) =>
            {
                initializationStarted.TrySetResult();
                return new ValueTask<DaemonLaunchSessionWriteResult>(releaseInitialization.Task);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher();
        var service = CreateService(
            launchSessionService,
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await initializationStarted.Task.WaitAsync(AsyncWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(AsyncWaitTimeout);

        timeProvider.Advance(timeout);
        var result = await launchTask.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(launcher.Invocations);
        Assert.Empty(compensationService.Invocations);

        releaseInitialization.TrySetResult(DaemonLaunchSessionWriteResult.Success(initializedSession));
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(context, compensationInvocation.UnityProject);
        Assert.Equal(initializedSession, compensationInvocation.ExpectedSession);
        Assert.Null(compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenBatchmodeProcessLaunchIgnoresCancellation_ReturnsAtDeadlineAndCleansLateProcess ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider();
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-late-batchmode-process-launch"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = timeProvider.GetUtcNow();
        const int processId = 8101;
        var launchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLaunch = new TaskCompletionSource<UnityDaemonLaunchResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            Handler = (_, _, _, _) =>
            {
                launchStarted.TrySetResult();
                return new ValueTask<UnityDaemonLaunchResult>(releaseLaunch.Task);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService
            {
                InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            },
            launcher,
            readinessProbe,
            compensationService,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await launchStarted.Task.WaitAsync(AsyncWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(AsyncWaitTimeout);

        timeProvider.Advance(timeout);
        var result = await launchTask.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(readinessProbe.Invocations);
        Assert.Empty(compensationService.Invocations);

        releaseLaunch.TrySetResult(UnityDaemonLaunchResult.Success(processId, processStartedAtUtc));
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(initialSession, compensationInvocation.ExpectedSession);
        Assert.Equal(
            new DaemonProcessTerminationTarget(processId, processStartedAtUtc),
            compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenProcessIdentityPersistenceIgnoresCancellation_ReturnsAtDeadlineAndCleansLateSession ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 07, 14, 0, 0, 2, TimeSpan.Zero));
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-late-process-identity-write"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = timeProvider.GetUtcNow();
        const int processId = 8102;
        var updatedSession = DaemonSessionTestFactory.Create(
            processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var persistenceStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePersistence = new TaskCompletionSource<DaemonLaunchSessionWriteResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdHandler = (_, _, _, _, _) =>
            {
                persistenceStarted.TrySetResult();
                return new ValueTask<DaemonLaunchSessionWriteResult>(releasePersistence.Task);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var service = CreateService(
            launchSessionService,
            new RecordingUnityDaemonProcessLauncher
            {
                NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
            },
            readinessProbe,
            compensationService,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await persistenceStarted.Task.WaitAsync(AsyncWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(AsyncWaitTimeout);

        timeProvider.Advance(timeout);
        var result = await launchTask.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(readinessProbe.Invocations);
        Assert.Empty(compensationService.Invocations);

        releasePersistence.TrySetResult(DaemonLaunchSessionWriteResult.Success(updatedSession));
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(updatedSession, compensationInvocation.ExpectedSession);
        Assert.Equal(
            new DaemonProcessTerminationTarget(processId, processStartedAtUtc),
            compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenGuiProcessLaunchIgnoresCancellation_ReturnsAtDeadlineAndCleansLateProcess ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 07, 14, 0, 0, 3, TimeSpan.Zero));
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-late-gui-process-launch"));
        var processStartedAtUtc = timeProvider.GetUtcNow();
        const int processId = 8103;
        var launchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLaunch = new TaskCompletionSource<UnityDaemonLaunchResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            Handler = (_, _, _) =>
            {
                launchStarted.TrySetResult();
                return new ValueTask<UnityDaemonLaunchResult>(releaseLaunch.Task);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Gui,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await launchStarted.Task.WaitAsync(AsyncWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(AsyncWaitTimeout);

        timeProvider.Advance(timeout);
        var result = await launchTask.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(guiStartupObserver.Invocations);
        Assert.Empty(compensationService.Invocations);

        releaseLaunch.TrySetResult(UnityDaemonLaunchResult.Success(processId, processStartedAtUtc));
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Null(compensationInvocation.ExpectedSession);
        Assert.Equal(
            new DaemonProcessTerminationTarget(processId, processStartedAtUtc),
            compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenCallerCancelsAfterSessionInitialization_DoesNotStartLaterPhasesAndCleansSession ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-cancel-after-session-initialization"));
        var initializedSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        using var cancellationSource = new CancellationTokenSource();
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeHandler = (_, _, _) =>
            {
                cancellationSource.Cancel();
                return ValueTask.FromResult(DaemonLaunchSessionWriteResult.Success(initializedSession));
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher();
        var progressObserver = new CollectingDaemonStartProgressObserver();
        var service = CreateService(
            launchSessionService,
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            timeProvider: timeProvider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LaunchAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    DaemonEditorMode.Batchmode,
                    DaemonStartupBlockedProcessPolicy.Auto,
                    progressObserver,
                    cancellationSource.Token)
                .AsTask());
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        progressObserver.AssertEvents();
        Assert.Empty(launcher.Invocations);
        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(initializedSession, compensationInvocation.ExpectedSession);
        Assert.Null(compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchingProgressIgnoresCancellation_ReturnsAtDeadlineWithoutStartingProcess ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-late-launching-progress"));
        var initializedSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var launchingProgressStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLaunchingProgress = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var progressEvents = new List<DaemonStartProgressEvent>();
        var progressObserver = new ConfigurableDaemonStartProgressObserver
        {
            Handler = (progressEvent, _) =>
            {
                progressEvents.Add(progressEvent);
                if (progressEvent != DaemonStartProgressEvent.Launching)
                {
                    return ValueTask.CompletedTask;
                }

                launchingProgressStarted.TrySetResult();
                return new ValueTask(releaseLaunchingProgress.Task);
            },
        };
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var compensationService = new RecordingDaemonLaunchCompensationService
        {
            OnCleanup = () => cleanupStarted.TrySetResult(),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService
            {
                InitializeResult = DaemonLaunchSessionWriteResult.Success(initializedSession),
            },
            launcher,
            new RecordingDaemonStartupReadinessProbe(),
            compensationService,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                progressObserver,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await launchingProgressStarted.Task.WaitAsync(AsyncWaitTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(AsyncWaitTimeout);

        timeProvider.Advance(timeout);
        var result = await launchTask.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(launcher.Invocations);
        Assert.Empty(compensationService.Invocations);

        releaseLaunchingProgress.TrySetResult();
        await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);

        Assert.Equal(
            [DaemonStartProgressEvent.SessionRegistered, DaemonStartProgressEvent.Launching],
            progressEvents);
        Assert.Empty(launcher.Invocations);
        var compensationInvocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(initializedSession, compensationInvocation.ExpectedSession);
        Assert.Null(compensationInvocation.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenDeadlineExpiresBetweenOwnedCompletionAndOwnerObservation_ReturnsAfterCompensationDeadlineAndObservesLateCleanupOnce ()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 14, 0, 0, 4, TimeSpan.Zero);
        var timeProvider = new AdvanceOnTimestampReadTimeProvider(
            processStartedAtUtc,
            timeout,
            advanceOnTimestampRead: 9);
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-owner-observation-race"));
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        const int processId = 8104;
        var updatedSession = DaemonSessionTestFactory.Create(
            processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
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
        var readinessProbe = new RecordingDaemonStartupReadinessProbe();
        var service = CreateService(
            launchSessionService,
            new RecordingUnityDaemonProcessLauncher
            {
                NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
            },
            readinessProbe,
            compensationService,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var launchTask = service.LaunchAsync(
                context,
                ExecutionDeadline.Start(timeout, timeProvider),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        try
        {
            await cleanupStarted.Task.WaitAsync(AsyncWaitTimeout);
            await timeProvider
                .WaitForTimerDueWithinAsync(DaemonTimeouts.LaunchCompensationTimeout)
                .WaitAsync(AsyncWaitTimeout);

            timeProvider.Advance(DaemonTimeouts.LaunchCompensationTimeout);
            var result = await launchTask.WaitAsync(AsyncWaitTimeout);

            Assert.Equal(DaemonStartStatus.Failed, result.Status);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Empty(readinessProbe.Invocations);
            var compensationInvocation = Assert.Single(compensationService.Invocations);
            Assert.Equal(updatedSession, compensationInvocation.ExpectedSession);
            Assert.Equal(
                new DaemonProcessTerminationTarget(processId, processStartedAtUtc),
                compensationInvocation.Target);
            Assert.True(compensationInvocation.CancellationToken.IsCancellationRequested);

            var quiescenceTask = compensationOperationOwner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(DaemonTimeouts.LaunchCompensationTimeout, timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for late launch compensation to quiesce.")
                .AsTask();
            Assert.False(quiescenceTask.IsCompleted);

            releaseCleanup.TrySetResult(DaemonSessionStoreOperationResult.Success());

            Assert.Null(await quiescenceTask.WaitAsync(AsyncWaitTimeout));
            Assert.Single(compensationService.Invocations);
        }
        finally
        {
            releaseCleanup.TrySetResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class AdvanceOnTimestampReadTimeProvider : TimeProvider
    {
        private readonly ManualTimeProvider inner;

        private readonly TimeSpan advanceBy;

        private readonly int advanceOnTimestampRead;

        private int timestampReadCount;

        public AdvanceOnTimestampReadTimeProvider (
            DateTimeOffset startUtc,
            TimeSpan advanceBy,
            int advanceOnTimestampRead)
        {
            inner = new ManualTimeProvider(startUtc);
            this.advanceBy = advanceBy;
            this.advanceOnTimestampRead = advanceOnTimestampRead;
        }

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow ()
        {
            return inner.GetUtcNow();
        }

        public override long GetTimestamp ()
        {
            var currentReadCount = Interlocked.Increment(ref timestampReadCount);
            if (currentReadCount == advanceOnTimestampRead)
            {
                inner.Advance(advanceBy);
            }

            return inner.GetTimestamp();
        }

        public void Advance (TimeSpan elapsed)
        {
            inner.Advance(elapsed);
        }

        public Task WaitForTimerDueWithinAsync (TimeSpan maximumDelay)
        {
            return inner.WaitForTimerDueWithinAsync(maximumDelay);
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }
}
