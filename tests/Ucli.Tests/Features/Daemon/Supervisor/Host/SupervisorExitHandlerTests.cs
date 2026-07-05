using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExitHandlerTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task HandleExit_WhenDiagnosisWriteAndCleanupFail_LogsBothFailures ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-exit-handler",
            "diagnosis-cleanup-failures",
            DirectoryCleanupMode.BestEffort);
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: "fingerprint");
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            ownerProcessId: 24);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(
                ExecutionError.InternalError("diagnosis failed")),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Failure(
                ExecutionError.InternalError("cleanup failed")),
        };
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            artifactCleaner,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new SupervisorRuntimeLogger());
        var managedProcess = new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            processId: -1,
            static _ => Task.CompletedTask);

        await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None);

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(scope.FullPath);
        var logText = await File.ReadAllTextAsync(logPath);
        Assert.Contains("Supervisor diagnosis write failed after daemon exit.", logText, StringComparison.Ordinal);
        Assert.Contains("diagnosis failed", logText, StringComparison.Ordinal);
        Assert.Contains("Supervisor artifact cleanup failed after daemon exit.", logText, StringComparison.Ordinal);
        Assert.Contains("cleanup failed", logText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task HandleExit_WhenSessionReadFails_StillRunsCleanupAndLogsReadFailure ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-exit-handler",
            "session-read-failure",
            DirectoryCleanupMode.BestEffort);
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: "fingerprint");
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            ownerProcessId: 24);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Failure(
                ExecutionError.InternalError("session read failed"),
                DaemonSessionReadFailureKind.IoFailure),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            artifactCleaner,
            new SupervisorDiagnosisWriter(new RecordingDaemonDiagnosisStore()),
            new SupervisorRuntimeLogger());
        var managedProcess = new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            processId: -1,
            static _ => Task.CompletedTask);

        await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None);

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(scope.FullPath);
        var logText = await File.ReadAllTextAsync(logPath);
        SupervisorExitHandlerAssert.SessionReadFailureLoggedAfterCleanup(
            artifactCleaner,
            unityProject,
            logText,
            expectedReadFailureMessage: "session read failed");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleExit_WhenSessionCannotShutdownProcess_SkipsDiagnosisAndCleanup ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: ResolvedUnityProjectContextTestFactory.RepositoryRoot,
            projectFingerprint: "fingerprint");
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            ownerProcessId: 24);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            new UnexpectedDaemonArtifactCleaner("User-owned GUI session must not be cleaned up by the supervisor exit handler."),
            new SupervisorDiagnosisWriter(new UnexpectedDaemonDiagnosisStore(
                "User-owned GUI session must not write unexpected-exit diagnosis.")),
            new SupervisorRuntimeLogger());
        var managedProcess = new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            processId: -1,
            static _ => Task.CompletedTask);

        await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None);
    }
}
