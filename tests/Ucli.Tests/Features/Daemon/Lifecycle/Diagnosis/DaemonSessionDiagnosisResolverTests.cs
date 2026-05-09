using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

public sealed class DaemonSessionDiagnosisResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenPersistedDiagnosisMatchesSession_ReturnsPersistedDiagnosis ()
    {
        var unityProject = CreateContext("fingerprint-resolver-match");
        var session = CreateSession(processId: 1234, unityProject.ProjectFingerprint);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, diagnosis, CancellationToken.None);

        Assert.Equal(diagnosis, result);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenPersistedDiagnosisDoesNotMatchAndProcessIsDead_ReturnsSynthesizedDiagnosis ()
    {
        var unityProject = CreateContext("fingerprint-resolver-synth");
        var session = CreateSession(processId: int.MaxValue, unityProject.ProjectFingerprint);
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);
        var mismatchedDiagnosis = CreateDiagnosis(
            session with
            {
                IssuedAtUtc = session.IssuedAtUtc.AddMinutes(-1),
            },
            DaemonDiagnosisReasonValues.ShutdownRequested);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, mismatchedDiagnosis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, result!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, result.ReportedBy);
        Assert.True(result.IsInferred);
        Assert.Equal(session.ProcessId, result.ProcessId);
        Assert.Equal(session.IssuedAtUtc, result.SessionIssuedAtUtc);
        Assert.Equal(result, diagnosisStore.LastDiagnosis);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenProcessIsStillAlive_ReturnsNull ()
    {
        var unityProject = CreateContext("fingerprint-resolver-alive");
        var session = CreateSession(processId: Environment.ProcessId, unityProject.ProjectFingerprint);
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, persistedDiagnosis: null, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenDiagnosisPersistenceFails_StillReturnsSynthesizedDiagnosis ()
    {
        var unityProject = CreateContext("fingerprint-resolver-write-fail");
        var session = CreateSession(processId: int.MaxValue, unityProject.ProjectFingerprint);
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("write failed")),
        };
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, persistedDiagnosis: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, result!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, result.ReportedBy);
        Assert.True(result.IsInferred);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: processId,

            OwnerProcessId: 9876);
    }

    private static DaemonDiagnosis CreateDiagnosis (
        DaemonSession session,
        string reason)
    {
        return new DaemonDiagnosis(
            Reason: reason,
            Message: $"diagnosis:{reason}",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 0, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            SessionIssuedAtUtc: session.IssuedAtUtc);
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int WriteCallCount { get; private set; }

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
