using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceInvariantTests
{
    public static TheoryData<string, object> InvalidTransitionResponses ()
    {
        var data = new TheoryData<string, object>();

        var readyBefore = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        data.Add(
            "entered generation did not advance",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.Entered,
                readyBefore,
                After: CreateSnapshot(
                    UnityEditorLifecycleState.PlayMode,
                    CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
                    playModeGeneration: 2),
                Observed: null,
                ApplicationState: null))));

        var alreadyEnteredBefore = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
            playModeGeneration: 9);
        data.Add(
            "already entered generation changed",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.AlreadyEntered,
                alreadyEnteredBefore,
                After: CreateSnapshot(
                    UnityEditorLifecycleState.PlayMode,
                    CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
                    playModeGeneration: 10),
                Observed: null,
                ApplicationState: null))));

        data.Add(
            "entered after snapshot is stopped",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.Entered,
                readyBefore,
                After: CreateSnapshot(
                    UnityEditorLifecycleState.Ready,
                    CreateStoppedPlayMode(),
                    playModeGeneration: 3),
                Observed: null,
                ApplicationState: null))));

        data.Add(
            "already entered before snapshot is stopped",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.AlreadyEntered,
                CreateSnapshot(
                    UnityEditorLifecycleState.Ready,
                    CreateStoppedPlayMode(),
                    playModeGeneration: 9),
                After: alreadyEnteredBefore,
                Observed: null,
                ApplicationState: null))));

        data.Add(
            "entered before snapshot is already playing",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.Entered,
                alreadyEnteredBefore,
                After: CreateSnapshot(
                    UnityEditorLifecycleState.PlayMode,
                    CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
                    playModeGeneration: 3),
                Observed: null,
                ApplicationState: null))));

        data.Add(
            "entered before snapshot is not ready stopped",
            CreateResponse(new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.Entered,
                CreateSnapshot(
                    UnityEditorLifecycleState.Compiling,
                    CreateStoppedPlayMode(),
                    playModeGeneration: 2),
                After: CreateSnapshot(
                    UnityEditorLifecycleState.PlayMode,
                    CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
                    playModeGeneration: 3),
                Observed: null,
                ApplicationState: null))));

        return data;
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2,
            projectFingerprint: ProjectFingerprintTestFactory.Create("other-project-fingerprint"));
        var after = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayMode(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true),
            playModeGeneration: 3,
            projectFingerprint: ProjectFingerprintTestFactory.Create("other-project-fingerprint"));
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Entered,
            before,
            After: after,
            Observed: null,
            ApplicationState: null));
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
