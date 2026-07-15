using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-running"));
        var session = DaemonSessionTestFactory.Create(processId: 2001, projectFingerprint: context.ProjectFingerprint);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReason.ShutdownRequested);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var pingResponse = IpcUnityEditorObservationTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(pingResponse);
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(sessionStore, pingInfoClient),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Same(pingResponse, result.PingResponse);
        Assert.Null(result.Error);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionTokenRotatesDuringProbe_RetriesOnceWithRefreshedSession ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-token-rotation"));
        var firstSession = DaemonSessionTestFactory.Create(
            processId: 2001,
            sessionToken: "first-token",
            projectFingerprint: context.ProjectFingerprint);
        var refreshedSession = DaemonSessionTestFactory.Create(
            processId: 2001,
            sessionToken: "refreshed-token",
            projectFingerprint: context.ProjectFingerprint,
            issuedAtUtc: firstSession.IssuedAtUtc.AddSeconds(1));
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(firstSession)
                : DaemonSessionReadResultTestFactory.Found(refreshedSession),
        };
        var pingResponse = IpcUnityEditorObservationTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "The first session token was replaced.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            pingResponse);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(sessionStore, pingInfoClient),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(refreshedSession, result.Session);
        Assert.Same(pingResponse, result.PingResponse);
        Assert.Equal(2, sessionStore.ReadInvocations.Count);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(firstSession, invocation.Session),
            invocation => Assert.Equal(refreshedSession, invocation.Session));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenReplacementSessionProbeFails_AttributesStaleResultToReplacementSession (
        bool probeTimesOut)
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create($"fingerprint-status-replacement-failure-{probeTimesOut}"));
        var observedSession = DaemonSessionTestFactory.Create(
            processId: Environment.ProcessId,
            sessionToken: "observed-token",
            projectFingerprint: context.ProjectFingerprint);
        var replacementSession = DaemonSessionTestFactory.Create(
            processId: Environment.ProcessId,
            sessionToken: "replacement-token",
            projectFingerprint: context.ProjectFingerprint,
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            endpointAddress: "replacement-endpoint");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(observedSession)
                : DaemonSessionReadResultTestFactory.Found(replacementSession),
        };
        Exception replacementFailure = probeTimesOut
            ? new TimeoutException("Replacement probe timed out.")
            : IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "The observed session token was replaced.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            replacementFailure);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(sessionStore, pingInfoClient),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(replacementSession, result.Session);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingTimesOut_ReturnsStale ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-timeout"));
        var session = DaemonSessionTestFactory.Create(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-timeout-mismatched-diagnosis"));
        var session = DaemonSessionTestFactory.Create(processId: Environment.ProcessId, projectFingerprint: context.ProjectFingerprint);
        var oldSession = DaemonSessionTestFactory.Create(
            processId: Environment.ProcessId,
            projectFingerprint: context.ProjectFingerprint,
            issuedAtUtc: session.IssuedAtUtc.AddSeconds(-1));
        var persistedDiagnosis = CreateDiagnosis(oldSession, DaemonDiagnosisReason.StartupFailed);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(persistedDiagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(new TimeoutException("probe timeout"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-stale"));
        var session = DaemonSessionTestFactory.Create(processId: 2003, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReason.ShutdownRequested);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(
                    IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionPingFailsUnexpectedly_ReturnsInternalError ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-ping-failure"));
        var session = DaemonSessionTestFactory.Create(processId: 2004, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(new InvalidOperationException("broken pipe"))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error?.Kind);
        Assert.Equal("Failed to probe daemon status. broken pipe", result.Error?.Message);
        Assert.Null(result.PingResponse);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleDiagnosisResolutionFailsUnexpectedly_ReturnsInternalError ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-diagnosis-failure"));
        var session = DaemonSessionTestFactory.Create(processId: 2005, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: new RecordingDaemonDiagnosisStore(),
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(
                    IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new ThrowingDaemonSessionDiagnosisResolver(
                new InvalidOperationException("diagnosis store failed")),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error?.Kind);
        Assert.Equal("Failed to resolve stale daemon diagnosis. diagnosis store failed", result.Error?.Message);
        Assert.Null(result.PingResponse);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionDoesNotExist_ReturnsPersistedDiagnosisWithNotRunning ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-not-running"));
        var diagnosis = CreateDiagnosis(DaemonSessionTestFactory.Create(processId: null, projectFingerprint: context.ProjectFingerprint), DaemonDiagnosisReason.StartupFailed);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Missing(),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new UnexpectedDaemonPingInfoClient("No session exists.")),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-last-attempt"));
        var lastLaunchAttempt = CreateLaunchAttempt(context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Missing(),
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
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new UnexpectedDaemonPingInfoClient("No session exists.")),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-external"));
        var session = DaemonSessionTestFactory.Create(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Success(null),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(
                    IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedBy.Cli, result.Diagnosis.ReportedBy);
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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-external-write-fail"));
        var session = DaemonSessionTestFactory.Create(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
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
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(
                    IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(DaemonDiagnosisReason.ExternalTerminationSuspected, result.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedBy.Cli, result.Diagnosis.ReportedBy);
        Assert.True(result.Diagnosis.IsInferred);
        DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisReadFails_StillReturnsRunningFromSessionAndPing ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-diagnosis-read-failure"));
        var session = DaemonSessionTestFactory.Create(processId: 2004, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            ReadResult = DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument("diagnosis malformed")),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(IpcUnityEditorObservationTestFactory.Create(projectFingerprint: context.ProjectFingerprint))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: new ManualTimeProvider());

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Null(result.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisReadStops_ReturnsTimeout ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-diagnosis-read-timeout"));
        var timeProvider = new ManualTimeProvider();
        var sessionStore = new RecordingDaemonSessionStore();
        var diagnosisStore = new BlockingDaemonDiagnosisReadStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new UnexpectedDaemonPingInfoClient("Diagnosis read must time out first.")),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var resultTask = operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisStore.ReadStarted, "Daemon status diagnosis read start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon status diagnosis read timeout result", TimeSpan.FromSeconds(5));

        AssertTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionReadStops_ReturnsTimeout ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-session-read-timeout"));
        var timeProvider = new ManualTimeProvider();
        var sessionReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, cancellationToken) =>
            {
                sessionReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new UnexpectedDaemonPingInfoClient("Session read must time out first.")),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var resultTask = operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(sessionReadStarted.Task, "Daemon status session read start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon status session read timeout result", TimeSpan.FromSeconds(5));

        AssertTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenLaunchAttemptReadStops_ReturnsTimeout ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-launch-attempt-read-timeout"));
        var timeProvider = new ManualTimeProvider();
        var launchAttemptStore = new BlockingDaemonLaunchAttemptStore();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Missing(),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: launchAttemptStore,
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new UnexpectedDaemonPingInfoClient("No session exists.")),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var resultTask = operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(launchAttemptStore.ReadStarted, "Daemon status launch-attempt read start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon status launch-attempt read timeout result", TimeSpan.FromSeconds(5));

        AssertTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenPingStops_ReturnsTimeout ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-ping-timeout"));
        var session = DaemonSessionTestFactory.Create(processId: 2601, projectFingerprint: context.ProjectFingerprint);
        var timeProvider = new ManualTimeProvider();
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var pingInfoClient = new RecordingDaemonPingInfoClient
        {
            PingAndReadHandler = async (_, _, _, _, cancellationToken) =>
            {
                pingStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(sessionStore, pingInfoClient),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var resultTask = operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(pingStarted.Task, "Daemon status ping start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon status ping timeout result", TimeSpan.FromSeconds(5));

        AssertTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisWriteStopsAfterUnreachablePing_ReturnsTimeout ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-diagnosis-write-timeout"));
        var session = DaemonSessionTestFactory.Create(processId: int.MaxValue, projectFingerprint: context.ProjectFingerprint);
        var timeProvider = new ManualTimeProvider();
        var diagnosisStore = new BlockingDaemonDiagnosisWriteStore();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(
                sessionStore,
                new RecordingDaemonPingInfoClient(
                    IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused))),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var resultTask = operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisStore.WriteStarted, "Daemon status diagnosis write start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon status diagnosis write timeout result", TimeSpan.FromSeconds(5));

        AssertTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_AfterMetadataReads_PassesRemainingTimeoutToPing ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-status-remaining-timeout"));
        var session = DaemonSessionTestFactory.Create(processId: 2602, projectFingerprint: context.ProjectFingerprint);
        var timeProvider = new ManualTimeProvider();
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            OnRead = (_, _) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(25));
                return DaemonDiagnosisReadResult.Success(null);
            },
        };
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            OnRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(25)),
        };
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            IpcUnityEditorObservationTestFactory.Create(projectFingerprint: context.ProjectFingerprint));
        var operation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonDiagnosisStore: diagnosisStore,
            launchAttemptStore: new RecordingDaemonLaunchAttemptStore(),
            daemonSessionProbe: CreateSessionProbe(sessionStore, pingInfoClient),
            reachabilityClassifier: new DaemonReachabilityClassifier(),
            daemonSessionDiagnosisResolver: new DaemonSessionDiagnosisResolver(diagnosisStore),
            timeProvider: timeProvider);

        var result = await operation.GetStatusAsync(context, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Running, result.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(50), Assert.Single(pingInfoClient.Invocations).Timeout);
    }

    private static DaemonSessionProbe CreateSessionProbe (
        IDaemonSessionStore sessionStore,
        IDaemonPingInfoClient pingInfoClient)
    {
        return new DaemonSessionProbe(
            sessionStore,
            pingInfoClient,
            new DaemonReachabilityClassifier());
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
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc,
            ProcessStartedAtUtc: null,
            UnityLogPath: null,
            StartupPhase: null,
            ActionRequired: null,
            PrimaryDiagnostic: null);
    }

    private static DaemonLaunchAttempt CreateLaunchAttempt (ProjectFingerprint projectFingerprint)
    {
        var diagnosis = CreateDiagnosis(DaemonSessionTestFactory.Create(processId: null, projectFingerprint: projectFingerprint), DaemonDiagnosisReason.StartupFailed);
        return new DaemonLaunchAttempt(
            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
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

    private static void AssertTimeout (DaemonStatusResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
    }

    private sealed class ThrowingDaemonSessionDiagnosisResolver : IDaemonSessionDiagnosisResolver
    {
        private readonly Exception exception;

        public ThrowingDaemonSessionDiagnosisResolver (Exception exception)
        {
            this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public ValueTask<DaemonDiagnosis?> ResolveForSessionAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            DaemonDiagnosis? persistedDiagnosis,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            ArgumentNullException.ThrowIfNull(session);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<DaemonDiagnosis?>(exception);
        }
    }

    private sealed class BlockingDaemonDiagnosisReadStore : IDaemonDiagnosisStore
    {
        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public async ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new System.Diagnostics.UnreachableException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingDaemonDiagnosisWriteStore : IDaemonDiagnosisStore
    {
        private readonly TaskCompletionSource writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteStarted => writeStarted.Task;

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public async ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writeStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new System.Diagnostics.UnreachableException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingDaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
    {
        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            DaemonLaunchAttempt launchAttempt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new System.Diagnostics.UnreachableException();
        }

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            int keepCount,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

}
