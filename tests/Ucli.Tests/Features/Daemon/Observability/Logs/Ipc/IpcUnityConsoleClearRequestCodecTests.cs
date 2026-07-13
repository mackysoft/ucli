using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityConsoleClearRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_ReturnsUnityConsoleClearRequestEnvelope ()
    {
        var request = IpcUnityConsoleClearRequestCodec.CreateRequest("session-token");

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.UnityConsoleClear), request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcUnityConsoleClearRequest payload, out _));
        Assert.Equal(UcliCommandIds.LogsUnityClear.Name, payload.RequestedBy);
    }
}
