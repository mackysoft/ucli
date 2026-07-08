using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceInvariantTests
{
    public static TheoryData<string, object> InvalidTransitionResponses ()
    {
        var data = new TheoryData<string, object>();

        var readyBefore = CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("2"));
        data.Add(
            "missing transition payload",
            CreateResponseWithoutTransitionPayload());

        data.Add(
            "invalid play mode state",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                readyBefore)
            {
                After = CreateSnapshot(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, false, new IpcPlayModeSnapshot(
                    State: "invalid",
                    Transition: "none",
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: "3")),
            })));

        data.Add(
            "entered generation did not advance",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                readyBefore)
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    IpcEditorBlockingReasonCodec.PlayMode,
                    false,
                    CreatePlayMode("playing", "none", true, true, "2")),
            })));

        var alreadyEnteredBefore = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreatePlayMode("playing", "none", true, true, "9"));
        data.Add(
            "already entered generation changed",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.AlreadyEntered,
                alreadyEnteredBefore)
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    IpcEditorBlockingReasonCodec.PlayMode,
                    false,
                    CreatePlayMode("playing", "none", true, true, "10")),
            })));

        data.Add(
            "success transition carried error fields",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                readyBefore)
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    IpcEditorBlockingReasonCodec.PlayMode,
                    false,
                    CreatePlayMode("playing", "none", true, true, "3")),
                Observed = readyBefore,
                ApplicationState = IpcPlayApplicationStateNames.NotApplied,
            })));

        data.Add(
            "entered after snapshot is stopped",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                readyBefore)
            {
                After = CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("3")),
            })));

        data.Add(
            "already entered before snapshot is stopped",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.AlreadyEntered,
                CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true, CreateStoppedPlayMode("9")))
            {
                After = alreadyEnteredBefore,
            })));

        data.Add(
            "blocked application state is invalid",
            CreateErrorResponse(
                new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                    IpcPlayTransitionCommandNames.Enter,
                    IpcPlayTransitionResultNames.Blocked,
                    CreateSnapshot(IpcEditorLifecycleStateCodec.Compiling, IpcEditorBlockingReasonCodec.Compile, false, CreateStoppedPlayMode("2")))
                {
                    Observed = CreateSnapshot(IpcEditorLifecycleStateCodec.Compiling, IpcEditorBlockingReasonCodec.Compile, false, CreateStoppedPlayMode("2")),
                    ApplicationState = "maybeApplied",
                }),
                PlayModeErrorCodes.PlayModeTransitionBlocked,
                "Unity Play Mode enter is blocked."));

        data.Add(
            "timeout application state is not indeterminate",
            CreateErrorResponse(
                new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                    IpcPlayTransitionCommandNames.Enter,
                    IpcPlayTransitionResultNames.Timeout,
                    readyBefore)
                {
                    Observed = CreateSnapshot(
                        IpcEditorLifecycleStateCodec.Playmode,
                        IpcEditorBlockingReasonCodec.PlayMode,
                        false,
                        CreatePlayMode("entering", "entering", false, true, "2")),
                    ApplicationState = IpcPlayApplicationStateNames.NotApplied,
                }),
                PlayModeErrorCodes.PlayModeTransitionTimeout,
                "Unity Play Mode enter timed out."));

        data.Add(
            "success snapshot missing play mode blocking reason",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                readyBefore)
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    blockingReason: null,
                    canAcceptExecutionRequests: false,
                    playMode: CreatePlayMode("playing", "none", true, true, "3")),
            })));

        data.Add(
            "entered before snapshot is already playing",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                alreadyEnteredBefore)
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    IpcEditorBlockingReasonCodec.PlayMode,
                    false,
                    CreatePlayMode("playing", "none", true, true, "3")),
            })));

        data.Add(
            "entered before snapshot is not ready stopped",
            CreateResponse(new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
                IpcPlayTransitionCommandNames.Enter,
                IpcPlayTransitionResultNames.Entered,
                CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Compiling,
                    IpcEditorBlockingReasonCodec.Compile,
                    false,
                    CreateStoppedPlayMode("2")))
            {
                After = CreateSnapshot(
                    IpcEditorLifecycleStateCodec.Playmode,
                    IpcEditorBlockingReasonCodec.PlayMode,
                    false,
                    CreatePlayMode("playing", "none", true, true, "3")),
            })));

        return data;
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            CreateStoppedPlayMode("2"),
            projectFingerprint: "other-project-fingerprint");
        var after = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreatePlayMode("playing", "none", true, true, "3"),
            projectFingerprint: "other-project-fingerprint");
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            IpcPlayTransitionResultNames.Entered,
            before)
        {
            After = after,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("projectFingerprint mismatch", result.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitionResponses))]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTransitionResponseViolatesEnterInvariant_ReturnsStateUnknown (
        string caseName,
        object response)
    {
        _ = caseName;
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(Assert.IsType<UnityRequestResponse>(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }
}
