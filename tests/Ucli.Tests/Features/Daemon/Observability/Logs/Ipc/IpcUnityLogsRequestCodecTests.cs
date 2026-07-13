using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityLogsRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_WithQueryValues_ReturnsUnityLogsReadRequestEnvelope ()
    {
        var query = new IpcUnityLogsReadRequest(
            Tail: 32,
            After: "stream-1:44",
            Since: "2026-03-05T01:00:00Z",
            Until: "2026-03-05T02:00:00Z",
            Level: "warning",
            Query: "socket",
            QueryTarget: "stack",
            Source: "runtime",
            StackTrace: "error",
            StackTraceMaxFrames: 8,
            StackTraceMaxChars: 4096);

        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);
        var request = IpcUnityLogsRequestCodec.CreateRequest(query, sessionToken);

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(sessionToken.GetEncodedValue(), request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.UnityLogsRead), request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcUnityLogsReadRequest payload, out _));
        Assert.Equal(32, payload.Tail);
        Assert.Equal("stream-1:44", payload.After);
        Assert.Equal("2026-03-05T01:00:00Z", payload.Since);
        Assert.Equal("2026-03-05T02:00:00Z", payload.Until);
        Assert.Equal("warning", payload.Level);
        Assert.Equal("socket", payload.Query);
        Assert.Equal("stack", payload.QueryTarget);
        Assert.Equal("runtime", payload.Source);
        Assert.Equal("error", payload.StackTrace);
        Assert.Equal(8, payload.StackTraceMaxFrames);
        Assert.Equal(4096, payload.StackTraceMaxChars);
    }
}
