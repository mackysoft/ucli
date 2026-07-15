using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

public sealed class DaemonSessionDiagnosisResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenPersistedDiagnosisMatchesSession_ReturnsPersistedDiagnosis ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-match"));
        var session = DaemonSessionTestFactory.Create(
            processId: 1234,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReason.ShutdownRequested);
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Matching persisted diagnosis should be returned without writing diagnosis.");
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, diagnosis, CancellationToken.None);

        Assert.Equal(diagnosis, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenPersistedDiagnosisProcessStartedAtUtcDoesNotMatch_ReturnsNullForLiveProcess ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-process-start-mismatch"));
        var session = DaemonSessionTestFactory.Create(
            processId: Environment.ProcessId,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReason.ShutdownRequested,
            Message: "diagnosis:shutdownRequested",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 0, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value.AddSeconds(-1),
            UnityLogPath: null,
            StartupPhase: null,
            ActionRequired: null,
            PrimaryDiagnostic: null);
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Live process with mismatched diagnosis should not write diagnosis.");
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, diagnosis, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenPersistedDiagnosisDoesNotMatchAndProcessIsDead_ReturnsSynthesizedDiagnosis ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-synth"));
        var session = DaemonSessionTestFactory.Create(
            processId: int.MaxValue,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);
        var mismatchedSession = DaemonSessionTestFactory.Create(
            processId: int.MaxValue,
            projectFingerprint: unityProject.ProjectFingerprint,
            issuedAtUtc: session.IssuedAtUtc.AddMinutes(-1),
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var mismatchedDiagnosis = CreateDiagnosis(
            mismatchedSession,
            DaemonDiagnosisReason.ShutdownRequested);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, mismatchedDiagnosis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, result!.Reason);
        Assert.Equal(DaemonDiagnosisReportedBy.Cli, result.ReportedBy);
        Assert.True(result.IsInferred);
        Assert.Equal(session.ProcessId, result.ProcessId);
        Assert.Equal(session.IssuedAtUtc, result.SessionIssuedAtUtc);
        Assert.Equal(session.ProcessStartedAtUtc, result.ProcessStartedAtUtc);
        var diagnosis = DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, unityProject);
        Assert.Equal(result, diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenProcessIsStillAlive_ReturnsNull ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-alive"));
        var session = DaemonSessionTestFactory.Create(
            processId: Environment.ProcessId,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Live process without persisted diagnosis should not write diagnosis.");
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, persistedDiagnosis: null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenDiagnosisPersistenceFails_StillReturnsSynthesizedDiagnosis ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-write-fail"));
        var session = DaemonSessionTestFactory.Create(
            processId: int.MaxValue,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("write failed")),
        };
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var result = await resolver.ResolveForSessionAsync(unityProject, session, persistedDiagnosis: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, result!.Reason);
        Assert.Equal(DaemonDiagnosisReportedBy.Cli, result.ReportedBy);
        Assert.True(result.IsInferred);
        DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveForSession_WhenCallerCancelsDuringDiagnosisWrite_RethrowsCancellation ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-resolver-write-cancellation"));
        var session = DaemonSessionTestFactory.Create(
            processId: int.MaxValue,
            projectFingerprint: unityProject.ProjectFingerprint,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock");
        using var cancellationTokenSource = new CancellationTokenSource();
        var diagnosisStore = new CancelingDaemonDiagnosisStore(cancellationTokenSource);
        var resolver = new DaemonSessionDiagnosisResolver(diagnosisStore);

        var exception = await Record.ExceptionAsync(async () =>
            await resolver.ResolveForSessionAsync(
                    unityProject,
                    session,
                    persistedDiagnosis: null,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(cancellationTokenSource.Token, diagnosisStore.WriteCancellationToken);
    }

    private static DaemonDiagnosis CreateDiagnosis (
        DaemonSession session,
        DaemonDiagnosisReason reason)
    {
        return new DaemonDiagnosis(
            Reason: reason,
            Message: $"diagnosis:{reason}",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 0, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            UnityLogPath: null,
            StartupPhase: null,
            ActionRequired: null,
            PrimaryDiagnostic: null);
    }

    private sealed class CancelingDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        public CancelingDaemonDiagnosisStore (CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        public CancellationToken WriteCancellationToken { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCancellationToken = cancellationToken;
            cancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

}
