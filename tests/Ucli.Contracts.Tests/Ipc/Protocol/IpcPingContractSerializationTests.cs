using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcPingContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
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
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Playmode)
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "playing")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true)
                .HasString("generation", "42"));
    }
}
