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

    public static readonly Guid PlaySessionEditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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
        string lifecycleState = IpcEditorLifecycleStateCodec.Playmode,
        string? blockingReason = IpcEditorBlockingReasonCodec.PlayMode,
        bool canAcceptExecutionRequests = false,
        string playModeState = "playing",
        bool isPlaying = true,
        bool isPlayingOrWillChangePlaymode = true)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            editorMode: "gui",
            lifecycleState: lifecycleState,
            blockingReason: blockingReason,
            compileState: IpcCompileStateCodec.Ready,
            compileGeneration: "12",
            domainReloadGeneration: "7",
            observedAtUtc: ObservedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)))
        {
            ServerVersion = "0.5.0",
            CanAcceptExecutionRequests = canAcceptExecutionRequests,
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
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: "none",
            CompileState: IpcCompileStateCodec.Ready,
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
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusOk,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: []));
    }

    public static UnityRequestResponse CreateErrorResponse (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusError,
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
