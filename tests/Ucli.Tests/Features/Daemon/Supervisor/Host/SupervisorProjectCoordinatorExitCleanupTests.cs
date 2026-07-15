using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorProjectCoordinatorTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectCoordinatorExitCleanupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task StopProject_WhenStopFails_DoesNotSuppressUnexpectedExitDiagnosis ()
    {
        using var daemonProcess = SupervisorOwnedDaemonProcess.Start();
        using var scope = CreateUnityProjectScope(nameof(StopProject_WhenStopFails_DoesNotSuppressUnexpectedExitDiagnosis));
        var unityProject = CreateUnityProject(scope);
        var session = daemonProcess.CreateSession();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                session,
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint)),
        };
        var pingClient = new RecordingDaemonPingClient();
        var stopOperation = new RecordingDaemonStopOperation
        {
            StopResult = DaemonStopResult.Failure(ExecutionError.Timeout("stop failed")),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            diagnosisStore,
            sessionStore);

        var ensureRunningResult = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);

        var stopResult = await coordinator.StopProjectAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
                CancellationToken.None);

        Assert.False(stopResult.IsSuccess);

        await daemonProcess.TerminateAndAwaitCoordinatorAsync(coordinator);

        var diagnosis = DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, unityProject);
        Assert.Equal(DaemonDiagnosisReason.UnexpectedExit, diagnosis.Reason);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ManagedProjectCount_RemainsNonZeroUntilExitCleanupCompletes ()
    {
        using var scope = CreateUnityProjectScope(nameof(ManagedProjectCount_RemainsNonZeroUntilExitCleanupCompletes));
        var unityProject = CreateUnityProject(scope);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = CreateExitedProcessSession();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                session,
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint)),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new RecordingDaemonStopOperation(),
            new RecordingDaemonPingClient(),
            new RecordingDaemonDiagnosisStore(),
            sessionStore,
            new RecordingDaemonArtifactCleaner
            {
                CleanupHandler = async (_, _) =>
                {
                    cleanupStarted.TrySetResult();
                    await cleanupRelease.Task.ConfigureAwait(false);
                    return DaemonArtifactCleanupResult.Success();
                },
            });

        try
        {
            var ensureRunningResult = await coordinator.EnsureRunningAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None);
            Assert.True(ensureRunningResult.IsSuccess);

            await TestAwaiter.WaitAsync(cleanupStarted.Task, "Managed project cleanup start", SignalWaitTimeout);
            Assert.True(coordinator.HasManagedProjects);
        }
        finally
        {
            cleanupRelease.TrySetResult();
            await coordinator.AwaitManagedProcessesAsync();
        }

        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AwaitManagedProcesses_WhenExitCleanupFaults_DetachesFaultedMonitorTask ()
    {
        using var scope = CreateUnityProjectScope(nameof(AwaitManagedProcesses_WhenExitCleanupFaults_DetachesFaultedMonitorTask));
        var unityProject = CreateUnityProject(scope);
        var session = CreateExitedProcessSession();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            ReadException = new InvalidOperationException("session read failed"),
        };
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                session,
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint)),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new RecordingDaemonStopOperation(),
            new RecordingDaemonPingClient(),
            new RecordingDaemonDiagnosisStore(),
            sessionStore);

        var ensureRunningResult = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);
        Assert.True(ensureRunningResult.IsSuccess);
        Assert.True(coordinator.HasManagedProjects);

        await coordinator.AwaitManagedProcessesAsync();

        Assert.False(coordinator.HasManagedProjects);
        Assert.False(coordinator.HasActiveProjectWork);
    }
}
