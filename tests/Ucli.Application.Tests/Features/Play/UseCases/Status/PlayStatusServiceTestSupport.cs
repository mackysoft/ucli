using System.Globalization;
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

    public static DaemonSession CreatePlaySession ()
    {
        return DaemonSessionTestFactory.CreateUserOwned(
            DaemonEditorMode.Gui,
            PlaySessionEndpointAddress,
            editorInstanceId: PlaySessionEditorInstanceId);
    }

    public static RecordingDaemonProcessIdentityAssessor CreateProcessIdentityAssessor (
        DaemonProcessIdentityAssessmentStatus status)
    {
        return new RecordingDaemonProcessIdentityAssessor(status);
    }

    public static DaemonLifecycleObservation CreateLifecycleObservation (
        DaemonSession session,
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.PlayMode,
        IpcPlayModeState playModeState = IpcPlayModeState.Playing,
        bool isPlaying = true,
        bool isPlayingOrWillChangePlaymode = true,
        long playModeGeneration = 9,
        Guid? editorInstanceId = null)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: lifecycleState,
                compileState: lifecycleState == IpcEditorLifecycleState.Compiling
                    ? IpcCompileState.Compiling
                    : IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 4,
                    PlayModeGeneration: playModeGeneration),
                playMode: new IpcPlayModeSnapshot(
                    State: playModeState,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: isPlaying,
                    IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode)),
            observedAtUtc: ObservedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: "0.5.0",
            editorInstanceId: editorInstanceId
                ?? session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            recoveryLease: null);
    }

    public static IpcPlayStatusResponse CreateStatusResponse (
        IpcPlayModeSnapshot? playMode = null,
        long playModeGeneration = 2,
        ProjectFingerprint? projectFingerprint = null)
    {
        return new IpcPlayStatusResponse(new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint ?? PlayProjectContext.UnityProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.Ready,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 4,
                    PlayModeGeneration: playModeGeneration),
                playMode: playMode ?? new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Stopped,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: ObservedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null));
    }

    public static UnityRequestResponse CreateResponse (IpcPlayStatusResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Ok,
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
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
