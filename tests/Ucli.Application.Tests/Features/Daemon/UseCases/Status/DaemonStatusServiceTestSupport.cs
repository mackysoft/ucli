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
        return new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.0.1",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileGeneration: "1",
            DomainReloadGeneration: "1",
            CanAcceptExecutionRequests: true));
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: IpcEditorLifecycleState.PlayMode,
            CompileState: IpcCompileState.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            ServerVersion = "0.5.0",
            EditorInstanceId = session.EditorInstanceId,
            PlayMode = new IpcPlayModeSnapshot(
                State: "playing",
                Transition: "none",
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true,
                Generation: "9"),
        };
    }
}
