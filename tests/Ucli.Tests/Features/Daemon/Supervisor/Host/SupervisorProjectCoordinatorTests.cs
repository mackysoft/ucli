using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorProjectCoordinatorTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectCoordinatorTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenDaemonIsAlreadyRunning_SkipsStabilityVerification ()
    {
        using var scope = CreateUnityProjectScope(nameof(EnsureRunning_WhenDaemonIsAlreadyRunning_SkipsStabilityVerification));
        var unityProject = CreateUnityProject(scope);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateAlreadyRunningResult(CreateExitedProcessSession()),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var coordinator = CreateCoordinator(
            startOperation,
            new UnexpectedDaemonStopOperation("Already-running daemon should not be stopped during ensure-running."),
            new UnexpectedDaemonPingClient("Already-running process-owned daemon should not use ping stability verification."),
            diagnosisStore,
            new RecordingDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);

        await coordinator.AwaitManagedProcessesAsync();
        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenOnStartupBlockedIsSpecified_PassesPolicyToStartOperation ()
    {
        var unityProject = CreateUnityProject();
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateAlreadyRunningResult(
                DaemonSessionTestFactory.Create(sessionToken: "session-token", processId: null)),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new RecordingDaemonStopOperation(),
            new RecordingDaemonPingClient(),
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonStartOperationAssert.EnsureRunningRequested(
            startOperation,
            unityProject.RepositoryRoot,
            unityProject.UnityProjectRoot,
            unityProject.ProjectFingerprint,
            TimeSpan.FromMilliseconds(500),
            expectedEditorMode: null,
            DaemonStartupBlockedProcessPolicy.Terminate);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenDaemonIsAlreadyRunningWithoutProcessId_TracksPidlessSessionUntilPingMonitorDetectsExit ()
    {
        using var scope = CreateUnityProjectScope(nameof(EnsureRunning_WhenDaemonIsAlreadyRunningWithoutProcessId_TracksPidlessSessionUntilPingMonitorDetectsExit));
        var unityProject = CreateUnityProject(scope);
        var releasePing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateAlreadyRunningResult(
                DaemonSessionTestFactory.Create(sessionToken: "session-token", processId: null)),
        };
        var pingClient = new RecordingDaemonPingClient(async (_, _, _, cancellationToken) =>
        {
            await releasePing.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            throw IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused);
        });
        var coordinator = CreateCoordinator(
            startOperation,
            new RecordingDaemonStopOperation(),
            pingClient,
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.True(coordinator.HasManagedProjects);

        releasePing.TrySetResult();
        await coordinator.AwaitManagedProcessesAsync();
        Assert.False(coordinator.HasManagedProjects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenUserOwnedGuiSessionIsAlreadyRunning_DoesNotRegisterManagedProcess ()
    {
        var unityProject = CreateUnityProject();
        var userOwnedSession = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            processId: 4242,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = CreateAlreadyRunningResult(userOwnedSession),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new RecordingDaemonStopOperation(),
            new UnexpectedDaemonPingClient("User-owned GUI session should not be monitored by the supervisor ping loop."),
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.False(coordinator.HasManagedProjects);
        Assert.False(coordinator.HasActiveProjectWork);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenUserOwnedGuiSessionStarts_DoesNotVerifyStabilityOrRegisterManagedProcess ()
    {
        var unityProject = CreateUnityProject();
        var userOwnedSession = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            processId: 4243,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(
                userOwnedSession,
                IpcUnityEditorObservationTestFactory.Create(
                    editorMode: DaemonEditorMode.Gui,
                    projectFingerprint: userOwnedSession.ProjectFingerprint)),
        };
        var coordinator = CreateCoordinator(
            startOperation,
            new UnexpectedDaemonStopOperation("User-owned GUI session should not be stopped by supervisor stability compensation."),
            new UnexpectedDaemonPingClient("User-owned GUI session should not use supervisor stability verification."),
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonSessionStore());

        var result = await coordinator.EnsureRunningAsync(
            unityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.Same(userOwnedSession, result.Session);
        Assert.False(coordinator.HasManagedProjects);
        Assert.False(coordinator.HasActiveProjectWork);
    }

    private static DaemonStartResult CreateAlreadyRunningResult (DaemonSession session)
    {
        return DaemonStartResult.AlreadyRunning(
            session,
            IpcUnityEditorObservationTestFactory.Create(
                editorMode: session.EditorMode,
                projectFingerprint: session.ProjectFingerprint));
    }
}
