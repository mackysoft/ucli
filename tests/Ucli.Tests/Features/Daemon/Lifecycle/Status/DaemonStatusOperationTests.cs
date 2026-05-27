using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingTimesOut_ReturnsStale ()
    {
        var context = CreateContext("fingerprint-status-timeout");
        var session = CreateSession(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Null(result.Diagnosis);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingTimesOutWithMismatchedPersistedDiagnosis_DoesNotReturnStaleDiagnosis ()
    {
        var context = CreateContext("fingerprint-status-timeout-mismatched-diagnosis");
        var session = CreateSession(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var oldSession = session with
        {
            IssuedAtUtc = session.IssuedAtUtc.AddSeconds(-1),
        };
        var persistedDiagnosis = CreateDiagnosis(oldSession, DaemonDiagnosisReasonValues.StartupFailed);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(persistedDiagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Null(result.Diagnosis);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.NotRunning, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionDoesNotExist_ReturnsLastLaunchAttempt ()
    {
        var context = CreateContext("fingerprint-status-last-attempt");
        var lastLaunchAttempt = CreateLaunchAttempt(context.ProjectFingerprint);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var launchAttemptStore = new StubDaemonLaunchAttemptStore
        {
            ReadResult = DaemonLaunchAttemptReadResult.Success(lastLaunchAttempt),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: launchAttemptStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.NotRunning, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(lastLaunchAttempt, result.LastLaunchAttempt);
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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            launchAttemptStore: new StubDaemonLaunchAttemptStore(),
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            ProcessStartedAtUtc: processId is null ? null : DateTimeOffset.UtcNow,
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
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc);
    }

    private static DaemonLaunchAttempt CreateLaunchAttempt (string projectFingerprint)
    {
        var diagnosis = CreateDiagnosis(CreateSession(processId: null, projectFingerprint: projectFingerprint), DaemonDiagnosisReasonValues.StartupFailed);
        return new DaemonLaunchAttempt(
            LaunchAttemptId: "20260312_000000Z_00000001",
            StartedAtUtc: diagnosis.UpdatedAtUtc,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            StartupStatus: DaemonStartupStatusValues.Failed,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Unknown,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown,
            ProcessAction: DaemonStartupProcessActionValues.None,
            EditorMode: "gui",
            ProcessId: null,
            ProcessStartedAtUtc: null,
            UnityLogPath: "/tmp/unity.log",
            ArtifactPath: "/tmp/startup-diagnosis.json",
            Diagnosis: diagnosis);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ValueTask> handler;

        public StubDaemonPingClient (Func<ValueTask> handler)
        {
            this.handler = handler;
        }

        public ValueTask PingAsync (
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

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
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
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
    {
        public DaemonLaunchAttemptReadResult ReadResult { get; set; } = DaemonLaunchAttemptReadResult.Success(null);

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonLaunchAttempt launchAttempt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
            string storageRoot,
            string projectFingerprint,
            int keepCount,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
