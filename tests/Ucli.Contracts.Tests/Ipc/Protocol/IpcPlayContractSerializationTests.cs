using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

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
        var before = CreatePlayLifecycleSnapshot("stopped", "none");
        var after = CreatePlayLifecycleSnapshot("playing", "none");
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
                .HasString("editorMode", "gui")
                .HasString("unityVersion", "6000.1.4f1")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("lifecycleState", "ready")
                .HasString("blockingReason", "none")
                .HasString("compileState", "idle")
                .HasBoolean("canAcceptExecutionRequests", true)
                .HasString("observedAtUtc", "2026-05-21T00:00:00+00:00")
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "stopped")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", false)
                    .HasBoolean("isPlayingOrWillChangePlaymode", false)
                    .HasString("generation", "42")));

        JsonAssert.For(transition)
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Enter)
                .HasString("result", IpcPlayTransitionResultNames.Entered)
                .HasString("applicationState", IpcPlayApplicationStateNames.Applied)
                .HasProperty("before", beforeSnapshot => beforeSnapshot
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "stopped")))
                .HasProperty("after", afterSnapshot => afterSnapshot
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "playing"))));

        var roundTrip = JsonSerializer.Deserialize<IpcPlayTransitionResponse>(
            transition.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(IpcPlayTransitionCommandNames.Enter, roundTrip.Transition.Transition);
        Assert.Equal(IpcPlayApplicationStateNames.Applied, roundTrip.Transition.ApplicationState);
    }

    private static IpcPlayLifecycleSnapshot CreatePlayLifecycleSnapshot (
        string playModeState,
        string transition)
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            LifecycleState: "ready",
            BlockingReason: "none",
            CompileState: "idle",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: playModeState,
                Transition: transition,
                IsPlaying: string.Equals(playModeState, "playing", StringComparison.Ordinal),
                IsPlayingOrWillChangePlaymode: string.Equals(playModeState, "playing", StringComparison.Ordinal)
                    || string.Equals(transition, "entering", StringComparison.Ordinal),
                Generation: "42"));
    }
}
