namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

internal static class DaemonExistingSessionGateServiceTestSupport
{
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
            timeProvider: timeProvider ?? TimeProvider.System);
    }

    public static DaemonLifecycleObservation CreateRecoveringObservation (
        DaemonSession session,
        DateTimeOffset? observedAtUtc = null)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: IpcEditorLifecycleStateCodec.Recovering,
            BlockingReason: IpcEditorBlockingReasonCodec.Recovery,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: observedAtUtc ?? DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

    public static DaemonSession CreateRecoveringGuiSession (
        int processId,
        string projectFingerprint,
        string editorInstanceId)
    {
        return DaemonSessionTestFactory.Create(
            processId: processId,
            projectFingerprint: projectFingerprint,
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false) with
        {
            EditorInstanceId = editorInstanceId,
        };
    }

    public static RecordingDaemonLifecycleStore CreateRecoveringLifecycleStore (
        DaemonSession session,
        DateTimeOffset? observedAtUtc = null)
    {
        return new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session, observedAtUtc)),
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
            ProjectFingerprint: "fingerprint",
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
