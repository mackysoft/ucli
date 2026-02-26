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

    [Fact]
    [Trait("Size", "Small")]
    public void IpcErrorCodes_ContainsInvalidArgumentConstant ()
    {
        Assert.Equal("INVALID_ARGUMENT", IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteCommandNames_ExposeExpectedCommandLiterals ()
    {
        Assert.Equal("validate", IpcExecuteCommandNames.Validate);
        Assert.Equal("plan", IpcExecuteCommandNames.Plan);
        Assert.Equal("call", IpcExecuteCommandNames.Call);
        Assert.Equal("resolve", IpcExecuteCommandNames.Resolve);
        Assert.Equal("query", IpcExecuteCommandNames.Query);
        Assert.Equal("refresh", IpcExecuteCommandNames.Refresh);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteCommandNames_ClassifiesKnownAndOperationPipelineCommands ()
    {
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Validate));
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Plan));
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Call));
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Resolve));
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Query));
        Assert.True(IpcExecuteCommandNames.IsKnown(IpcExecuteCommandNames.Refresh));
        Assert.False(IpcExecuteCommandNames.IsKnown("unknown"));

        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Validate));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Plan));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Call));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Resolve));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Query));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(IpcExecuteCommandNames.Refresh));
        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand("unknown"));
    }

}