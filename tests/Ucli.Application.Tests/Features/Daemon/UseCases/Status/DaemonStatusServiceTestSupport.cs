using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonStatusServiceTestSupport
{
    public static DaemonStatusService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        TimeProvider? timeProvider = null)
    {
        return CreateService(
            resolver,
            daemonStatusOperation,
            CreateSuccessfulPingInfoClient(),
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            timeProvider);
    }

    public static DaemonStatusService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient pingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionDiagnosisResolver diagnosisResolver,
        TimeProvider? timeProvider = null,
        IDaemonLifecycleStore? lifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null)
    {
        return new DaemonStatusService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            reachabilityClassifier,
            lifecycleStore ?? new RecordingDaemonLifecycleStore(),
            processIdentityAssessor ?? new RecordingDaemonProcessIdentityAssessor(),
            diagnosisResolver,
            new DaemonSessionOutputMapper(),
            new DaemonDiagnosisOutputMapper(),
            timeProvider);
    }

    public static RecordingDaemonPingInfoClient CreateSuccessfulPingInfoClient ()
    {
        return new RecordingDaemonPingInfoClient(IpcUnityEditorObservationTestFactory.Create());
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        bool includeEditorInstanceId = true)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: IpcEditorLifecycleState.PlayMode,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(12, 7, 0, 9),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Playing,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true)),
            observedAtUtc: DateTimeOffset.UtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: "0.5.0",
            editorInstanceId: includeEditorInstanceId ? session.EditorInstanceId : null);
    }
}
