using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Play;

internal static class PlayEnterServiceTestSupport
{
    public const string PlaySessionEndpointAddress = "ucli-play-enter";

    public static ProjectContext PlayProjectContext { get; } = ProjectContextTestFactory.CreateSingleRootProject();

    public static PlayEnterService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        return CreateService(ProjectContextResolutionResult.Success(context), sessionStore, requestExecutor);
    }

    public static PlayEnterService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StaticProjectContextResolver(contextResult),
            sessionStore);
        return new PlayEnterService(contextResolver, requestExecutor);
    }

    public static RecordingDaemonSessionStore CreateGuiSessionStore ()
    {
        return new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.CreateUserOwned(
                DaemonEditorMode.Gui,
                PlaySessionEndpointAddress,
                DaemonSessionTestFactory.DefaultEditorInstanceId)));
    }

    public static IpcPlayTransitionResponse CreateEnteredResponse ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleState.Ready, CreateStoppedPlayMode(), playModeGeneration: 2);
        var after = CreateSnapshot(IpcEditorLifecycleState.PlayMode, CreatePlayMode(
            IpcPlayModeState.Playing,
            IpcPlayModeTransition.None,
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true),
            playModeGeneration: 3);
        return new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommand.Enter,
            IpcPlayTransitionOutcome.Entered,
            before,
            After: after,
            Observed: null,
            ApplicationState: null));
    }

    public static IpcUnityEditorObservation CreateSnapshot (
        IpcEditorLifecycleState lifecycleState,
        IpcPlayModeSnapshot playMode,
        long playModeGeneration,
        ProjectFingerprint? projectFingerprint = null)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint ?? PlayProjectContext.UnityProject.ProjectFingerprint,
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
                playMode: playMode),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static IpcPlayModeSnapshot CreateStoppedPlayMode ()
    {
        return CreatePlayMode(
            IpcPlayModeState.Stopped,
            IpcPlayModeTransition.None,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
    }

    public static IpcPlayModeSnapshot CreatePlayMode (
        IpcPlayModeState state,
        IpcPlayModeTransition transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        return new IpcPlayModeSnapshot(
            State: state,
            Transition: transition,
            IsPlaying: isPlaying,
            IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode);
    }

    public static UnityRequestResponse CreateResponse (IpcPlayTransitionResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: []));
    }

    public static UnityRequestResponse CreateInvalidPayloadResponse ()
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement("invalid"),
            errors: []));
    }

    public static UnityRequestResponse CreateErrorResponse (
        IpcPlayTransitionResponse payload,
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    public static UnityRequestResponse CreateErrorResponseWithoutTransitionPayload (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(new
            {
                ignored = true,
            }),
            errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
