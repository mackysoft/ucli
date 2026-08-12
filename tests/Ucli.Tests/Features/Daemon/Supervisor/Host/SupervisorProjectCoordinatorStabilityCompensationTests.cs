using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorProjectCoordinatorTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectCoordinatorStabilityCompensationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityVerificationIsCanceled_StopsStartedDaemonAndKeepsItManaged ()
    {
        using var daemonProcess = SupervisorOwnedDaemonProcess.Start();
        using var scope = CreateUnityProjectScope(nameof(EnsureRunning_WhenStabilityVerificationIsCanceled_StopsStartedDaemonAndKeepsItManaged));
        var unityProject = CreateUnityProject(scope);
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateStartedResult(daemonProcess),
        };
        var pingClient = new RecordingDaemonPingClient(async (_, _, _, cancellationToken) =>
        {
            pingStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        });
        var stopOperation = new RecordingDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            new RecordingDaemonSessionStore());
        using var cancellationTokenSource = new CancellationTokenSource();

        var ensureRunningTask = coordinator.EnsureRunningAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: cancellationTokenSource.Token)
            .AsTask();
        try
        {
            await pingStarted.Task.WaitAsync(SignalWaitTimeout);
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await ensureRunningTask.WaitAsync(SignalWaitTimeout);
            });

            await stopStarted.Task.WaitAsync(SignalWaitTimeout);
            DaemonStopOperationAssert.CompensationStopAttempted(
                stopOperation,
                unityProject,
                DaemonTimeouts.StopCompensationTimeout);
            Assert.True(coordinator.HasManagedProjects);
            Assert.True(coordinator.HasActiveProjectWork);
        }
        finally
        {
            stopRelease.TrySetResult();
            await daemonProcess.TerminateAndAwaitCoordinatorAsync(coordinator);
        }

        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityTimesOut_ReturnsBeforeBackgroundCompensationCompletes ()
    {
        using var daemonProcess = SupervisorOwnedDaemonProcess.Start();
        using var scope = CreateUnityProjectScope(nameof(EnsureRunning_WhenStabilityTimesOut_ReturnsBeforeBackgroundCompensationCompletes));
        var unityProject = CreateUnityProject(scope);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateStartedResult(daemonProcess),
        };
        var pingClient = new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            return ValueTask.CompletedTask;
        });
        var stopOperation = new RecordingDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            new RecordingDaemonSessionStore(),
            timeProvider: timeProvider);

        try
        {
            var result = await coordinator.EnsureRunningAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(70), timeProvider),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.True(coordinator.HasActiveProjectWork);
            await stopStarted.Task.WaitAsync(SignalWaitTimeout);
        }
        finally
        {
            stopRelease.TrySetResult();
            await daemonProcess.TerminateAndAwaitCoordinatorAsync(coordinator);
        }

        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenStabilityFails_ReturnsBeforeBackgroundCompensationCompletes ()
    {
        using var daemonProcess = SupervisorOwnedDaemonProcess.Start();
        using var scope = CreateUnityProjectScope(nameof(EnsureRunning_WhenStabilityFails_ReturnsBeforeBackgroundCompensationCompletes));
        var unityProject = CreateUnityProject(scope);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateStartedResult(daemonProcess),
        };
        var pingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new InvalidOperationException("ping failed")));
        var stopOperation = new RecordingDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore());

        try
        {
            var result = await coordinator.EnsureRunningAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
            Assert.True(coordinator.HasActiveProjectWork);
            await stopStarted.Task.WaitAsync(SignalWaitTimeout);
        }
        finally
        {
            stopRelease.TrySetResult();
            await daemonProcess.TerminateAndAwaitCoordinatorAsync(coordinator);
        }

        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StopProject_WhenBackgroundCompensationIsStillRunning_RespectsCallerTimeout ()
    {
        using var daemonProcess = SupervisorOwnedDaemonProcess.Start();
        using var scope = CreateUnityProjectScope(nameof(StopProject_WhenBackgroundCompensationIsStillRunning_RespectsCallerTimeout));
        var unityProject = CreateUnityProject(scope);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateStartedResult(daemonProcess),
        };
        var pingClient = new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            return ValueTask.CompletedTask;
        });
        var stopOperation = new RecordingDaemonStopOperation
        {
            StopHandler = async (_, _, _) =>
            {
                stopStarted.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
                return DaemonStopResult.Stopped();
            },
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore(),
            timeProvider: timeProvider);

        try
        {
            var ensureRunningResult = await coordinator.EnsureRunningAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(70), timeProvider),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None);
            Assert.False(ensureRunningResult.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, ensureRunningResult.Error!.Kind);
            await stopStarted.Task.WaitAsync(SignalWaitTimeout);

            var stopTask = coordinator.StopProjectAsync(
                    unityProject,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), TimeProvider.System),
                    CancellationToken.None)
                .AsTask();
            var stopResult = await stopTask.WaitAsync(SignalWaitTimeout);

            SupervisorProjectCoordinatorAssert.StopProjectTimedOutWhileCompensationStopIsRunning(
                stopResult,
                coordinator,
                stopOperation,
                unityProject);
        }
        finally
        {
            stopRelease.TrySetResult();
            await daemonProcess.TerminateAndAwaitCoordinatorAsync(coordinator);
        }

        Assert.False(coordinator.HasActiveProjectWork);
    }

    private static DaemonStartResult CreateStartedResult (SupervisorOwnedDaemonProcess daemonProcess)
    {
        var session = daemonProcess.CreateSession();
        return DaemonStartResult.Started(
            session,
            UnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint));
    }

}
