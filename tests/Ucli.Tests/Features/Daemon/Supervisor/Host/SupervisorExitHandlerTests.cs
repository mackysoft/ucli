using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExitHandlerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleExit_WhenDiagnosisWriteAndCleanupFail_LogsBothFailures ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-exit-handler-tests", Guid.NewGuid().ToString("N"));
        var unityProject = CreateUnityProject(repositoryRoot);
        var session = CreateSession();
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(
                ExecutionError.InternalError("diagnosis failed")),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            CleanupResult = DaemonSessionStoreOperationResult.Failure(
                ExecutionError.InternalError("cleanup failed")),
        };
        var exitHandler = new SupervisorExitHandler(
            new StubDaemonSessionStore(session),
            artifactCleaner,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new SupervisorRuntimeLogger());
        var managedProcess = new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            processId: -1,
            static _ => Task.CompletedTask);

        await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None);

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(repositoryRoot);
        var logText = await File.ReadAllTextAsync(logPath);
        Assert.Contains("Supervisor diagnosis write failed after daemon exit.", logText, StringComparison.Ordinal);
        Assert.Contains("diagnosis failed", logText, StringComparison.Ordinal);
        Assert.Contains("Supervisor artifact cleanup failed after daemon exit.", logText, StringComparison.Ordinal);
        Assert.Contains("cleanup failed", logText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleExit_WhenSessionReadFails_StillRunsCleanupAndLogsReadFailure ()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-exit-handler-tests", Guid.NewGuid().ToString("N"));
        var unityProject = CreateUnityProject(repositoryRoot);
        var session = CreateSession();
        var sessionStore = new StubDaemonSessionStore(session)
        {
            ReadResult = DaemonSessionReadResult.Failure(
                ExecutionError.InternalError("session read failed"),
                DaemonSessionReadFailureKind.IoFailure),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner();
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            artifactCleaner,
            new SupervisorDiagnosisWriter(new StubDaemonDiagnosisStore()),
            new SupervisorRuntimeLogger());
        var managedProcess = new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            processId: -1,
            static _ => Task.CompletedTask);

        await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None);

        Assert.Equal(1, artifactCleaner.CleanupCallCount);

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(repositoryRoot);
        var logText = await File.ReadAllTextAsync(logPath);
        Assert.Contains("Supervisor session read failed during exit cleanup.", logText, StringComparison.Ordinal);
        Assert.Contains("session read failed", logText, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateUnityProject (string repositoryRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 1234,
            OwnerProcessId: 24);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private DaemonSession session;

        public DaemonSessionReadResult? ReadResult { get; set; }

        public StubDaemonSessionStore (DaemonSession session)
        {
            this.session = session;
        }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult ?? DaemonSessionReadResult.Success(session));
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            this.session = session;
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

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } =
            DaemonDiagnosisStoreOperationResult.Success();

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
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public DaemonSessionStoreOperationResult CleanupResult { get; set; } =
            DaemonSessionStoreOperationResult.Success();

        public int CleanupCallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CleanupCallCount++;
            return ValueTask.FromResult(CleanupResult);
        }
    }
}
