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
        return new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned("gui", PlaySessionEndpointAddress)));
    }

    public static IpcPlayTransitionResponse CreateExitedResponse ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot("ready", null, true, CreateStoppedPlayMode("3"));
        return new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
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
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode);
    }

    public static IpcPlayModeSnapshot CreatePlayingPlayMode (string generation)
    {
        return new IpcPlayModeSnapshot(
            State: "playing",
            Transition: "none",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: generation);
    }

    public static IpcPlayModeSnapshot CreateStoppedPlayMode (string generation)
    {
        return new IpcPlayModeSnapshot(
            State: "stopped",
            Transition: "none",
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false,
            Generation: generation);
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
