namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Runtime;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

public sealed class DaemonStatusOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingSucceeds_ReturnsRunning ()
    {
        var context = CreateContext("fingerprint-status-running");
        var session = CreateSession(processId: 2001, projectFingerprint: context.ProjectFingerprint);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingTimesOut_ReturnsTimeoutFailure ()
    {
        var context = CreateContext("fingerprint-status-timeout");
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateSession(processId: 2002, projectFingerprint: context.ProjectFingerprint)),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("Timed out while probing daemon status.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingReturnsNotRunningException_ReturnsStale ()
    {
        var context = CreateContext("fingerprint-status-stale");
        var session = CreateSession(processId: 2003, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionDoesNotExist_ReturnsPersistedDiagnosisWithNotRunning ()
    {
        var context = CreateContext("fingerprint-status-not-running");
        var diagnosis = CreateDiagnosis(CreateSession(processId: null, projectFingerprint: context.ProjectFingerprint), DaemonDiagnosisReasonValues.StartupFailed);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.NotRunning, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleWithoutPersistedDiagnosis_DerivesExternalTerminationDiagnosis ()
    {
        var context = CreateContext("fingerprint-status-external");
        var session = CreateSession(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(null),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, result.Diagnosis.ReportedBy);
        Assert.True(result.Diagnosis.IsInferred);
        Assert.Equal(session.ProcessId, result.Diagnosis.ProcessId);
        Assert.Equal(session.IssuedAtUtc, result.Diagnosis.SessionIssuedAtUtc);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.Equal(result.Diagnosis, diagnosisStore.LastDiagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenExternalTerminationDiagnosisPersistenceFails_StillReturnsSynthesizedDiagnosis ()
    {
        var context = CreateContext("fingerprint-status-external-write-fail");
        var session = CreateSession(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(null),
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("write failed")),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, result.Diagnosis.ReportedBy);
        Assert.True(result.Diagnosis.IsInferred);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisReadFails_StillReturnsRunningFromSessionAndPing ()
    {
        var context = CreateContext("fingerprint-status-diagnosis-read-failure");
        var session = CreateSession(processId: 2004, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument("diagnosis malformed")),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatus(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Null(result.Diagnosis);
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
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
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
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            SessionIssuedAtUtc: session.IssuedAtUtc);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ValueTask> handler;

        public StubDaemonPingClient (Func<ValueTask> handler)
        {
            this.handler = handler;
        }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            return handler();
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosisReadResult ReadResult { get; set; } = DaemonDiagnosisReadResult.Success(null);

        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int WriteCallCount { get; private set; }

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastDiagnosis = diagnosis;
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
}