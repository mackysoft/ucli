using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Play;

internal static class PlayExitServiceTestSupport
{
    public const string PlaySessionEndpointAddress = "ucli-play-exit";

    public static readonly ProjectContext PlayProjectContext = ProjectContextTestFactory.CreateSingleRootProject();

    public static PlayExitService CreateService (
        ProjectContext context,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        return CreateService(ProjectContextResolutionResult.Success(context), sessionStore, requestExecutor);
    }

    public static PlayExitService CreateService (
        ProjectContextResolutionResult contextResult,
        IDaemonSessionStore sessionStore,
        IUnityRequestExecutor requestExecutor)
    {
        var contextResolver = new PlayCommandExecutionContextResolver(
            new StaticProjectContextResolver(contextResult),
            sessionStore);
        return new PlayExitService(contextResolver, requestExecutor);
    }

    public static RecordingDaemonSessionStore CreateGuiSessionStore ()
    {
        return new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned(DaemonEditorMode.Gui, PlaySessionEndpointAddress)));
    }

    public static IpcPlayTransitionResponse CreateExitedResponse ()
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var after = CreateSnapshot(
            IpcEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 3);
        return new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
    }

    public static IpcUnityEditorObservation CreateSnapshot (
        IpcEditorLifecycleState lifecycleState,
        IpcPlayModeSnapshot playMode,
        long playModeGeneration,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint,
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

    public static IpcPlayModeSnapshot CreatePlayingPlayMode ()
    {
        return new IpcPlayModeSnapshot(
            State: IpcPlayModeState.Playing,
            Transition: IpcPlayModeTransition.None,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true);
    }

    public static IpcPlayModeSnapshot CreateStoppedPlayMode ()
    {
        return new IpcPlayModeSnapshot(
            State: IpcPlayModeState.Stopped,
            Transition: IpcPlayModeTransition.None,
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false);
    }

    public static UnityRequestResponse CreateResponse (IpcPlayTransitionResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: []));
    }

    public static UnityRequestResponse CreateErrorResponse (
        IpcPlayTransitionResponse payload,
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors:
            [
                new IpcError(code, message, null),
            ]));
    }

    public static UnityRequestResponse CreateErrorResponseWithoutTransitionPayload (
        UcliCode code,
        string message)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-1",
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new
            {
                ignored = true,
            }),
            Errors:
            [
                new IpcError(code, message, null),
            ]));
    }
}
