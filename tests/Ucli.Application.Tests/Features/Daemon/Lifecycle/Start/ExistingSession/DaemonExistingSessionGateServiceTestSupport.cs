namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

internal static class DaemonExistingSessionGateServiceTestSupport
{
    private static readonly DateTimeOffset DefaultUtcNow = new(
        2026,
        03,
        12,
        00,
        00,
        00,
        TimeSpan.Zero);

    public static DaemonExistingSessionGateService CreateService (
        IDaemonPingInfoClient? daemonPingInfoClient = null,
        IDaemonReachabilityClassifier? reachabilityClassifier = null,
        IDaemonSessionCleanupService? cleanupService = null,
        IDaemonLifecycleStore? lifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonExistingSessionGateService(
            daemonPingInfoClient: daemonPingInfoClient ?? new RecordingDaemonPingInfoClient(CreateReadyPingResponse()),
            reachabilityClassifier: reachabilityClassifier ?? new StubDaemonReachabilityClassifier(static _ => false),
            daemonSessionCleanupService: cleanupService ?? new RecordingDaemonSessionCleanupService(),
            daemonLifecycleStore: lifecycleStore ?? new RecordingDaemonLifecycleStore(),
            processIdentityAssessor: processIdentityAssessor ?? new RecordingDaemonProcessIdentityAssessor(),
            timeProvider: timeProvider ?? new ManualTimeProvider(DefaultUtcNow));
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests,
        DateTimeOffset? observedAtUtc = null)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            editorMode: ContractLiteralCodec.ToValue(session.EditorMode),
            lifecycleState: lifecycleState,
            blockingReason: blockingReason,
            compileState: IpcCompileStateCodec.Ready,
            compileGeneration: "1",
            domainReloadGeneration: "2",
            observedAtUtc: observedAtUtc ?? DefaultUtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            canAcceptExecutionRequests: canAcceptExecutionRequests,
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            playMode: null);
    }

    public static DaemonSession CreateRecoveringGuiSession (
        int processId,
        ProjectFingerprint projectFingerprint,
        Guid editorInstanceId)
    {
        return DaemonSessionTestFactory.Create(
            processId: processId,
            projectFingerprint: projectFingerprint,
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            editorInstanceId: editorInstanceId);
    }

    public static RecordingDaemonLifecycleStore CreateRecoveringLifecycleStore (
        DaemonSession session,
        DateTimeOffset? observedAtUtc = null)
    {
        return new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(
                session,
                IpcEditorLifecycleStateCodec.Recovering,
                IpcEditorBlockingReasonCodec.Recovery,
                canAcceptExecutionRequests: false,
                observedAtUtc: observedAtUtc)),
        };
    }

    public static RecordingDaemonProcessIdentityAssessor CreateMatchingProcessIdentityAssessor (DaemonSession session)
    {
        return new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                session.ProcessStartedAtUtc,
                Error: null),
        };
    }

    public static IpcPingResponse CreatePingResponse (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests)
    {
        return new IpcPingResponse(
            ServerVersion: "1.0.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.0f1",
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    public static IpcPingResponse CreateReadyPingResponse ()
    {
        return CreatePingResponse(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true);
    }
}
