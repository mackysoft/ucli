using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.Shared.Context.Project;

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

        await exitHandler.HandleExit(managedProcess, CancellationToken.None);

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

        await exitHandler.HandleExit(managedProcess, CancellationToken.None);

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
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
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

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult ?? DaemonSessionReadResult.Success(session));
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            this.session = session;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
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

        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
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

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CleanupCallCount++;
            return ValueTask.FromResult(CleanupResult);
        }
    }
}