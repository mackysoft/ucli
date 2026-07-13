using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

public sealed class DaemonStatusOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingSucceeds_ReturnsRunning ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-running");
        var session = DaemonSessionTestFactory.Create(processId: 2001, projectFingerprint: context.ProjectFingerprint);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(),
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-timeout");
        var session = DaemonSessionTestFactory.Create(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new TimeoutException("probe timeout"))),
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-timeout-mismatched-diagnosis");
        var session = DaemonSessionTestFactory.Create(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var oldSession = session with
        {
            IssuedAtUtc = session.IssuedAtUtc.AddSeconds(-1),
        };
        var persistedDiagnosis = CreateDiagnosis(oldSession, DaemonDiagnosisReasonValues.StartupFailed);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(persistedDiagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonStatusOperationAssert.StaleSessionReturnedWithoutDiagnosisWrite(
            result,
            session,
            diagnosisStore);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingReturnsNotRunningException_ReturnsStale ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-stale");
        var session = DaemonSessionTestFactory.Create(processId: 2003, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-not-running");
        var diagnosis = CreateDiagnosis(DaemonSessionTestFactory.Create(processId: null, projectFingerprint: context.ProjectFingerprint), DaemonDiagnosisReasonValues.StartupFailed);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(),
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-last-attempt");
        var lastLaunchAttempt = CreateLaunchAttempt(context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(null),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore
        {
            ReadResult = DaemonLaunchAttemptReadResult.Success(lastLaunchAttempt),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: launchAttemptStore,
            daemonPingClient: new RecordingDaemonPingClient(),
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-external");
        var session = DaemonSessionTestFactory.Create(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(null),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
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
        var diagnosis = DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, context);
        Assert.Equal(result.Diagnosis, diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenExternalTerminationDiagnosisPersistenceFails_StillReturnsSynthesizedDiagnosis ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-external-write-fail");
        var session = DaemonSessionTestFactory.Create(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(null),
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError("write failed")),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, result.Diagnosis.ReportedBy);
        Assert.True(result.Diagnosis.IsInferred);
        DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisReadFails_StillReturnsRunningFromSessionAndPing ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-status-diagnosis-read-failure");
        var session = DaemonSessionTestFactory.Create(processId: 2004, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument("diagnosis malformed")),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonPingClient: new RecordingDaemonPingClient(),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore));

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Null(result.Diagnosis);
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
        var diagnosis = CreateDiagnosis(DaemonSessionTestFactory.Create(processId: null, projectFingerprint: projectFingerprint), DaemonDiagnosisReasonValues.StartupFailed);
        return new DaemonLaunchAttempt(
            LaunchAttemptId: "20260312_000000Z_00000001",
            StartedAtUtc: diagnosis.UpdatedAtUtc,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            StartupStatus: DaemonStartupStatus.Failed,
            StartupBlockingReason: DaemonStartupBlockingReason.Unknown,
            RetryDisposition: DaemonStartupRetryDisposition.Unknown,
            ProcessAction: DaemonStartupProcessAction.None,
            EditorMode: DaemonEditorMode.Gui,
            ProcessId: null,
            ProcessStartedAtUtc: null,
            UnityLogPath: "/tmp/unity.log",
            ArtifactPath: "/tmp/startup-diagnosis.json",
            Diagnosis: diagnosis);
    }

}
