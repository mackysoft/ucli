using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Play;

internal static class PlayStatusServiceTestSupport
{
    public const string PlaySessionEndpointAddress = "ucli-play-status";

    public const string PlaySessionEditorInstanceId = "editor-instance-1";

    public static readonly DateTimeOffset ObservedAtUtc = DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture);

    public static readonly ProjectContext PlayProjectContext = ProjectContextTestFactory.CreateSingleRootProject();

    public static PlayStatusService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null)
    {
        return CreateService(
            ProjectContextResolutionResult.Success(context),
            sessionStore,
            requestExecutor,
            daemonLifecycleStore,
            processIdentityAssessor);
    }

    public static PlayStatusService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StaticProjectContextResolver(contextResult),
            sessionStore);
        return new PlayStatusService(
            contextResolver,
            requestExecutor,
            daemonLifecycleStore ?? new RecordingDaemonLifecycleStore(),
            processIdentityAssessor ?? CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.NotRunning),
            new ManualTimeProvider(ObservedAtUtc));
    }

    public static DaemonSession CreatePlaySession (string editorMode = "gui")
    {
        return DaemonSessionTestFactory.CreateUserOwned(
            editorMode,
            PlaySessionEndpointAddress,
            editorInstanceId: PlaySessionEditorInstanceId);
    }

    public static RecordingDaemonProcessIdentityAssessor CreateProcessIdentityAssessor (
        DaemonProcessIdentityAssessmentStatus status)
    {
        return new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                status,
                ObservedStartTimeUtc: null,
                Error: null),
        };
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.PlayMode,
        string playModeState = "playing",
        bool isPlaying = true,
        bool isPlayingOrWillChangePlaymode = true)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: "gui",
            LifecycleState: lifecycleState,
            CompileState: IpcCompileState.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            ObservedAtUtc: ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            ServerVersion = "0.5.0",
            EditorInstanceId = session.EditorInstanceId,
            PlayMode = new IpcPlayModeSnapshot(
                State: playModeState,
                Transition: "none",
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode,
                Generation: "9"),
        };
    }

    public static IpcPlayStatusResponse CreateStatusResponse (
        IpcPlayModeSnapshot? playMode = null,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPlayStatusResponse(new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode ?? new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2")));
    }

    public static UnityRequestResponse CreateResponse (IpcPlayStatusResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: []));
    }

    public static UnityRequestResponse CreateErrorResponse (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
