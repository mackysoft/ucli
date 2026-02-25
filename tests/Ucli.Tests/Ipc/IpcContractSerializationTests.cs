using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcContractSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_SerializesWithCamelCaseContractFields ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            command = "status",
        }, SerializerOptions);
        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            SessionToken: "token",
            Method: IpcMethodNames.Execute,
            Payload: payload);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(request, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("protocolVersion", IpcProtocol.CurrentVersion)
            .HasString("requestId", "req-1")
            .HasString("sessionToken", "token")
            .HasString("method", IpcMethodNames.Execute)
            .HasValueKind("payload", JsonValueKind.Object);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_RoundTripsWithErrors ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            result = "ok",
        }, SerializerOptions);
        var response = new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-2",
            Status: IpcProtocol.StatusError,
            Payload: payload,
            Errors:
            [
                new IpcError(IpcErrorCodes.CommandNotImplemented, "Not implemented", null),
            ]);

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<IpcResponse>(json, SerializerOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Status, roundTrip.Status);
        Assert.Single(roundTrip.Errors);
        Assert.Equal(IpcErrorCodes.CommandNotImplemented, roundTrip.Errors[0].Code);
        Assert.Equal("Not implemented", roundTrip.Errors[0].Message);
    }
}
