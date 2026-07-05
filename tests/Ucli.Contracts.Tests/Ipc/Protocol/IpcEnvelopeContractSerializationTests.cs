using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcEnvelopeContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_SerializesWithCamelCaseContractFields ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new
        {
            command = "status",
        });
        var request = new IpcRequest(
            ProtocolVersion: 1,
            RequestId: "req-1",
            SessionToken: "token",
            Method: "execute",
            Payload: payload,
            responseMode: IpcResponseMode.Single);

        var json = IpcPayloadCodec.SerializeToElement(request);
        JsonAssert.For(json)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", "req-1")
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
            .HasString("responseMode", ContractLiteralCodec.ToValue(IpcResponseMode.Single))
            .HasValueKind("payload", JsonValueKind.Object);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_RoundTripsWithErrors ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new
        {
            result = "ok",
        });
        var response = new IpcResponse(
            ProtocolVersion: 1,
            RequestId: "req-2",
            Status: "error",
            Payload: payload,
            Errors:
            [
                new IpcError(UcliCoreErrorCodes.CommandNotImplemented, "Not implemented", null),
            ]);

        var json = IpcPayloadCodec.SerializeToElement(response);
        Assert.Equal("COMMAND_NOT_IMPLEMENTED", json.GetProperty("errors")[0].GetProperty("code").GetString());

        var roundTrip = json.Deserialize<IpcResponse>(IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Status, roundTrip.Status);
        Assert.Single(roundTrip.Errors);
        Assert.Equal(UcliCoreErrorCodes.CommandNotImplemented, roundTrip.Errors[0].Code);
        Assert.Equal("Not implemented", roundTrip.Errors[0].Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_ProgressSerializesWithCamelCaseContractFields ()
    {
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            "req-stream",
            IpcStreamFrameKinds.Progress,
            TestRunProgressEventNames.RunStarted,
            IpcPayloadCodec.SerializeToElement(new TestRunStartedEntry(
                "run-1",
                "editmode",
                "Namespace.Tests",
                ["Assembly.Tests"],
                ["smoke"])),
            Response: null);

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasInt32("protocolVersion", IpcProtocol.CurrentVersion)
            .HasString("requestId", "req-stream")
            .HasString("kind", IpcStreamFrameKinds.Progress)
            .HasString("event", TestRunProgressEventNames.RunStarted)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("response", JsonValueKind.Null);
        JsonAssert.For(json.GetProperty("payload"))
            .HasString("runId", "run-1")
            .HasString("testPlatform", "editmode")
            .HasString("testFilter", "Namespace.Tests")
            .HasArrayLength("assemblyNames", 1)
            .HasArrayLength("testCategories", 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_TerminalSerializesWithResponse ()
    {
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            "req-stream",
            IpcStreamFrameKinds.Terminal,
            Event: null,
            IpcPayloadCodec.SerializeToElement(new { }),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                "req-stream",
                IpcProtocol.StatusOk,
                IpcPayloadCodec.SerializeToElement(new { exitCode = 0 }),
                Array.Empty<IpcError>()));

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasString("kind", IpcStreamFrameKinds.Terminal)
            .HasValueKind("event", JsonValueKind.Null)
            .HasValueKind("response", JsonValueKind.Object);
        JsonAssert.For(json.GetProperty("response"))
            .HasString("requestId", "req-stream")
            .HasString("status", IpcProtocol.StatusOk)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasArrayLength("errors", 0);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":null,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":123,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":"","message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":"lowercase_code","message":"bad","opId":null}]}""")]
    public void IpcResponse_WhenErrorCodeJsonIsInvalid_Throws (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(json, IpcJsonSerializerOptions.Default));
    }
}
