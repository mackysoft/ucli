using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcPingContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcPingRequest_SerializesFailFastOnlyWhenSpecified ()
    {
        var defaultRequest = new IpcPingRequest(IpcPingClientVersions.OneshotStartup);
        var defaultJson = IpcPayloadCodec.SerializeToElement(defaultRequest);

        Assert.Equal(IpcPingClientVersions.OneshotStartup, defaultJson.GetProperty("clientVersion").GetString());
        Assert.False(defaultJson.TryGetProperty("failFast", out _));

        var failFastRequest = new IpcPingRequest(IpcPingClientVersions.Ready, FailFast: true);
        var failFastJson = IpcPayloadCodec.SerializeToElement(failFastRequest);

        Assert.Equal(IpcPingClientVersions.Ready, failFastJson.GetProperty("clientVersion").GetString());
        Assert.True(failFastJson.GetProperty("failFast").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityEditorObservation_SerializesEstablishedLifecycleWireShape ()
    {
        var response = new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: "project-fingerprint",
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Batchmode,
                lifecycleState: IpcEditorLifecycleState.PlayMode,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 8,
                    PlayModeGeneration: 42),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Playing,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true)),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"));

        var json = IpcPayloadCodec.SerializeToElement(response);

        Assert.Equal(
            [
                "serverVersion",
                "unityVersion",
                "projectFingerprint",
                "state",
                "observedAtUtc",
                "actionRequired",
                "primaryDiagnostic",
            ],
            json.EnumerateObject().Select(static property => property.Name).ToArray());

        JsonAssert.For(json)
            .HasString("serverVersion", "0.5.0")
            .HasString("unityVersion", "6000.1.4f1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasString("observedAtUtc", "2026-05-21T00:00:00+00:00")
            .HasProperty("state", state => state
                .HasString("editorMode", "batchmode")
                .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.PlayMode))
                .HasString("compileState", "ready")
                .HasProperty("generations", generations => generations
                    .HasInt32("compileGeneration", 12)
                    .HasInt32("domainReloadGeneration", 7)
                    .HasInt32("assetRefreshGeneration", 8)
                    .HasInt32("playModeGeneration", 42))
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "playing")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", true)
                    .HasBoolean("isPlayingOrWillChangePlaymode", true)));

        Assert.Equal(JsonValueKind.Null, json.GetProperty("actionRequired").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("primaryDiagnostic").ValueKind);
    }
}
