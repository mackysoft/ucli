using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityConsoleClearRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_ReturnsUnityConsoleClearRequestEnvelope ()
    {
        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);
        var request = IpcUnityConsoleClearRequestCodec.CreateRequest(sessionToken);

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(sessionToken.GetEncodedValue(), request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.UnityConsoleClear), request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcUnityConsoleClearRequest payload, out _));
        Assert.Equal(UcliCommandIds.LogsUnityClear.Name, payload.RequestedBy);
    }
}
