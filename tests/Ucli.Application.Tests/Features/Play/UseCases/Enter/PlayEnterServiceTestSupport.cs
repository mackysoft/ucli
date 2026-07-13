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
        return new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.CreateUserOwned(
            "gui",
            PlaySessionEndpointAddress,
            DaemonSessionTestFactory.DefaultEditorInstanceId)));
    }

    public static IpcPlayTransitionResponse CreateEnteredResponse ()
    {
        var before = CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("2"));
        var after = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, CreatePlayMode(
            "playing",
            "none",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            generation: "3"));
        return new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            IpcPlayTransitionResultNames.Entered,
            before)
        {
            After = after,
        });
    }

    public static IpcPlayLifecycleSnapshot CreateSnapshot (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests,
        IpcPlayModeSnapshot playMode,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode);
    }

    public static IpcPlayModeSnapshot CreateStoppedPlayMode (string generation)
    {
        return CreatePlayMode(
            "stopped",
            "none",
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false,
            generation: generation);
    }

    public static IpcPlayModeSnapshot CreatePlayMode (
        string state,
        string transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode,
        string generation)
    {
        return new IpcPlayModeSnapshot(
            State: state,
            Transition: transition,
            IsPlaying: isPlaying,
            IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode,
            Generation: generation);
    }

    public static UnityRequestResponse CreateResponse (IpcPlayTransitionResponse payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusOk,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: []));
    }

    public static UnityRequestResponse CreateResponseWithoutTransitionPayload ()
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusOk,
            payload: IpcPayloadCodec.SerializeToElement(new
            {
                transition = (object?)null,
            }),
            errors: []));
    }

    public static UnityRequestResponse CreateInvalidPayloadResponse ()
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusOk,
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
            status: IpcProtocol.StatusError,
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
            status: IpcProtocol.StatusError,
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
