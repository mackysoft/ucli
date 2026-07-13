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
            timeProvider: timeProvider);
    }

    public static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: IpcEditorLifecycleState.Recovering,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId);
    }

    public static DaemonSession CreateRecoveringGuiSession (
        int processId,
        string projectFingerprint,
        string editorInstanceId)
    {
        return DaemonSessionTestFactory.Create(
            processId: processId,
            projectFingerprint: projectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false) with
        {
            EditorInstanceId = editorInstanceId,
        };
    }

    public static RecordingDaemonLifecycleStore CreateRecoveringLifecycleStore (DaemonSession session)
    {
        return new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
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

    public static IpcUnityEditorObservation CreatePingResponse (IpcEditorLifecycleState lifecycleState)
    {
        return IpcUnityEditorObservationTestFactory.Create(
            lifecycleState,
            projectFingerprint: "fingerprint");
    }

    public static IpcUnityEditorObservation CreateReadyPingResponse ()
    {
        return CreatePingResponse(IpcEditorLifecycleState.Ready);
    }
}
