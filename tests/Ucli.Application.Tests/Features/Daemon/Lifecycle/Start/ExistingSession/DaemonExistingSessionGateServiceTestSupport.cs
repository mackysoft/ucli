namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

internal static class DaemonExistingSessionGateServiceTestSupport
{
    private static readonly DateTimeOffset DefaultUtcNow = DateTimeOffset.UnixEpoch;

    public static DaemonExistingSessionGateService CreateService (
        IDaemonPingInfoClient? daemonPingInfoClient = null,
        IDaemonReachabilityClassifier? reachabilityClassifier = null,
        IDaemonSessionCleanupService? cleanupService = null,
        IDaemonLifecycleStore? lifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null,
        IDaemonSessionStore? daemonSessionStore = null)
    {
        var effectiveSessionStore = daemonSessionStore ?? new RecordingDaemonSessionStore();
        var effectiveReachabilityClassifier = reachabilityClassifier
            ?? new StubDaemonReachabilityClassifier(static _ => false);
        return new DaemonExistingSessionGateService(
            daemonSessionProbe: new DaemonSessionProbe(
                DaemonSessionAcquisitionCoordinatorTestFactory.Create(effectiveSessionStore),
                daemonPingInfoClient ?? new RecordingDaemonPingInfoClient(CreateReadyPingResponse()),
                effectiveReachabilityClassifier),
            reachabilityClassifier: effectiveReachabilityClassifier,
            daemonSessionCleanupService: cleanupService ?? new RecordingDaemonSessionCleanupService(),
            daemonLifecycleStore: lifecycleStore ?? new RecordingDaemonLifecycleStore(),
            processIdentityAssessor: processIdentityAssessor ?? new RecordingDaemonProcessIdentityAssessor());
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        IpcEditorLifecycleState lifecycleState,
        DateTimeOffset? observedAtUtc = null,
        Guid? editorInstanceId = null)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: lifecycleState,
                compileState: lifecycleState switch
                {
                    IpcEditorLifecycleState.Compiling => IpcCompileState.Compiling,
                    IpcEditorLifecycleState.CompileFailed => IpcCompileState.Failed,
                    _ => IpcCompileState.Ready,
                },
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc ?? DefaultUtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: editorInstanceId
                ?? session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            recoveryLease: null);
    }

    public static DaemonSession CreateRecoveringGuiSession (
        int processId,
        ProjectFingerprint projectFingerprint,
        Guid editorInstanceId)
    {
        return DaemonSessionTestFactory.Create(
            processId: processId,
            projectFingerprint: projectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
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
                IpcEditorLifecycleState.Recovering,
                observedAtUtc: observedAtUtc)),
        };
    }

    public static RecordingDaemonProcessIdentityAssessor CreateMatchingProcessIdentityAssessor (DaemonSession session)
    {
        return new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = DaemonProcessIdentityAssessment.MatchingLiveProcess(
                session.ProcessStartedAtUtc!.Value),
        };
    }

    public static IpcUnityEditorObservation CreatePingResponse (IpcEditorLifecycleState lifecycleState)
    {
        return IpcUnityEditorObservationTestFactory.Create(
            lifecycleState,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
    }

    public static IpcUnityEditorObservation CreateReadyPingResponse ()
    {
        return CreatePingResponse(IpcEditorLifecycleState.Ready);
    }
}
