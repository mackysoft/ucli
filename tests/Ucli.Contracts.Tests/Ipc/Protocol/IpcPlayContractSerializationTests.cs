using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcPlayContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayRequestContracts_SerializeWithCamelCaseFields ()
    {
        var statusRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayStatusRequest());
        var enterRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest { TimeoutMilliseconds = 1500 });
        var exitRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest());

        Assert.Equal(JsonValueKind.Object, statusRequest.ValueKind);
        Assert.Empty(statusRequest.EnumerateObject());
        JsonAssert.For(enterRequest)
            .HasInt32("timeoutMilliseconds", 1500);
        Assert.False(exitRequest.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayResponseContracts_SerializeWithCamelCaseFields ()
    {
        var before = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);
        var after = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);
        var statusResponse = new IpcPlayStatusResponse(before);
        var transitionResponse = new IpcPlayTransitionResponse(
            new IpcPlayTransitionResult(
                Transition: IpcPlayTransitionCommandNames.Enter,
                Result: IpcPlayTransitionResultNames.Entered,
                Before: before)
            {
                After = after,
                ApplicationState = IpcPlayApplicationStateNames.Applied,
            });

        var status = IpcPayloadCodec.SerializeToElement(statusResponse);
        var transition = IpcPayloadCodec.SerializeToElement(transitionResponse);

        JsonAssert.For(status)
            .HasProperty("snapshot", snapshot => snapshot
                .HasString("serverVersion", "0.5.0")
                .HasString("unityVersion", "6000.1.4f1")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("observedAtUtc", "2026-05-21T00:00:00+00:00")
                .HasProperty("state", state => state
                    .HasString("editorMode", "gui")
                    .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready))
                    .HasString("compileState", ContractLiteralCodec.ToValue(IpcCompileState.Ready))
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 12)
                        .HasInt32("domainReloadGeneration", 7)
                        .HasInt32("assetRefreshGeneration", 8)
                        .HasInt32("playModeGeneration", 42))
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "stopped")
                        .HasString("transition", "none")
                        .HasBoolean("isPlaying", false)
                        .HasBoolean("isPlayingOrWillChangePlaymode", false))));

        JsonAssert.For(transition)
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Enter)
                .HasString("result", IpcPlayTransitionResultNames.Entered)
                .HasString("applicationState", IpcPlayApplicationStateNames.Applied)
                .HasProperty("before", beforeSnapshot => beforeSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "stopped"))))
                .HasProperty("after", afterSnapshot => afterSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "playing")))));

        var roundTrip = JsonSerializer.Deserialize<IpcPlayTransitionResponse>(
            transition.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(IpcPlayTransitionCommandNames.Enter, roundTrip.Transition.Transition);
        Assert.Equal(IpcPlayApplicationStateNames.Applied, roundTrip.Transition.ApplicationState);
    }

    private static IpcUnityEditorObservation CreateObservation (
        IpcPlayModeState playModeState,
        IpcPlayModeTransition transition)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: "project-fingerprint",
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.Ready,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 8,
                    PlayModeGeneration: 42),
                playMode: new IpcPlayModeSnapshot(
                    State: playModeState,
                    Transition: transition,
                    IsPlaying: playModeState == IpcPlayModeState.Playing,
                    IsPlayingOrWillChangePlaymode: playModeState == IpcPlayModeState.Playing
                        || transition == IpcPlayModeTransition.Entering)),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }
}
