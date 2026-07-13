using MackySoft.Tests;
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
    public void IpcPingResponse_SerializesPlayModeSnapshotWithCamelCaseFields ()
    {
        var response = new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: ContractLiteralCodec.ToValue(IpcCompileState.Ready),
            LifecycleState: ContractLiteralCodec.ToValue(IpcEditorLifecycleState.PlayMode),
            BlockingReason: ContractLiteralCodec.ToValue(IpcEditorBlockingReason.PlayMode),
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: false,
            PlayMode: new IpcPlayModeSnapshot(
                State: "playing",
                Transition: "none",
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true,
                Generation: "42"));

        var json = IpcPayloadCodec.SerializeToElement(response);

        JsonAssert.For(json)
            .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.PlayMode))
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "playing")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true)
                .HasString("generation", "42"));
    }
}
