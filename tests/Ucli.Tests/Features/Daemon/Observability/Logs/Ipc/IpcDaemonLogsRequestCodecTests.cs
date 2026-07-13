using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcDaemonLogsRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_WithQueryValues_ReturnsDaemonLogsReadRequestEnvelope ()
    {
        var query = new IpcDaemonLogsReadRequest(
            Tail: 32,
            After: "stream-1:44",
            Since: "2026-03-05T01:00:00Z",
            Until: "2026-03-05T02:00:00Z",
            Level: "warning",
            Query: "socket",
            QueryTarget: "message",
            Category: "transport");

        var request = IpcDaemonLogsRequestCodec.CreateRequest(query, "session-token");

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.DaemonLogsRead), request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcDaemonLogsReadRequest payload, out _));
        Assert.Equal(32, payload.Tail);
        Assert.Equal("stream-1:44", payload.After);
        Assert.Equal("2026-03-05T01:00:00Z", payload.Since);
        Assert.Equal("2026-03-05T02:00:00Z", payload.Until);
        Assert.Equal("warning", payload.Level);
        Assert.Equal("socket", payload.Query);
        Assert.Equal("message", payload.QueryTarget);
        Assert.Equal("transport", payload.Category);
    }
}
