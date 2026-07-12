using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcEnvelopeContractSerializationTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(IpcRequest.ProtocolVersion))]
    [InlineData(nameof(IpcRequest.RequestId))]
    [InlineData(nameof(IpcRequest.SessionToken))]
    [InlineData(nameof(IpcRequest.Method))]
    [InlineData(nameof(IpcRequest.Payload))]
    [InlineData(nameof(IpcRequest.ResponseMode))]
    public void IpcRequest_EnvelopePropertiesAreConstructorOnly (string propertyName)
    {
        var property = typeof(IpcRequest).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_SerializesWithCamelCaseContractFields ()
    {
        var requestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
        var payload = IpcPayloadCodec.SerializeToElement(new
        {
            command = "status",
        });
        var request = new IpcRequest(
            protocolVersion: 1,
            requestId: requestId,
            sessionToken: "token",
            method: "execute",
            payload: payload,
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));

        var json = IpcPayloadCodec.SerializeToElement(request);
        JsonAssert.For(json)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", requestId.ToString("D"))
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
            .HasString("responseMode", ContractLiteralCodec.ToValue(IpcResponseMode.Single))
            .HasValueKind("payload", JsonValueKind.Object);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_RoundTripsWithErrors ()
    {
        var requestId = Guid.Parse("4b977408-e66e-48eb-bcc5-24ea5bce9b62");
        var payload = IpcPayloadCodec.SerializeToElement(new
        {
            result = "ok",
        });
        var response = new IpcResponse(
            protocolVersion: 1,
            requestId: requestId,
            status: "error",
            payload: payload,
            errors:
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
    public void IpcResponse_WhenRequestIdIsMissing_Throws ()
    {
        const string Json = """{"protocolVersion":1,"status":"error","payload":{},"errors":[]}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(Json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_WhenRequestIdIsExplicitlyNull_DeserializesWithoutCorrelation ()
    {
        const string Json = """{"protocolVersion":1,"requestId":null,"status":"error","payload":{},"errors":[]}""";

        var response = JsonSerializer.Deserialize<IpcResponse>(Json, IpcJsonSerializerOptions.Default);

        Assert.NotNull(response);
        Assert.Null(response.RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_ProgressSerializesWithCamelCaseContractFields ()
    {
        var requestId = Guid.Parse("cbd61a0f-a8db-42cf-ad3e-fb5cb558ab87");
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKinds.Progress,
            TestRunProgressEventNames.RunStarted,
            IpcPayloadCodec.SerializeToElement(new TestRunStartedEntry(
                "run-1",
                "editmode",
                "Namespace.Tests",
                ["Assembly.Tests"],
                ["smoke"])),
            response: null);

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasInt32("protocolVersion", IpcProtocol.CurrentVersion)
            .HasString("requestId", requestId.ToString("D"))
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
        var requestId = Guid.Parse("68387bcd-4b6c-4249-9cce-00318f600034");
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKinds.Terminal,
            @event: null,
            IpcPayloadCodec.SerializeToElement(new { }),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                requestId,
                IpcProtocol.StatusOk,
                IpcPayloadCodec.SerializeToElement(new { exitCode = 0 }),
                Array.Empty<IpcError>()));

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasString("kind", IpcStreamFrameKinds.Terminal)
            .HasValueKind("event", JsonValueKind.Null)
            .HasValueKind("response", JsonValueKind.Object);
        JsonAssert.For(json.GetProperty("response"))
            .HasString("requestId", requestId.ToString("D"))
            .HasString("status", IpcProtocol.StatusOk)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasArrayLength("errors", 0);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_WhenNestedResponseRequestIdDiffers_RejectsConstruction ()
    {
        var frameRequestId = Guid.Parse("b41f241d-26f5-49c9-8995-f1f0efe5c74e");
        var responseRequestId = Guid.Parse("4fa3d662-ec2d-4113-97fe-2aa5ac73acf5");
        var payload = IpcPayloadCodec.SerializeToElement(new { });
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            responseRequestId,
            IpcProtocol.StatusOk,
            payload,
            Array.Empty<IpcError>());

        Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            frameRequestId,
            IpcStreamFrameKinds.Terminal,
            @event: null,
            payload,
            response));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_WhenNestedResponseRequestIdDiffersInJson_RejectsDeserialization ()
    {
        const string Json = """
            {
              "protocolVersion": 1,
              "requestId": "b41f241d-26f5-49c9-8995-f1f0efe5c74e",
              "kind": "terminal",
              "event": null,
              "payload": {},
              "response": {
                "protocolVersion": 1,
                "requestId": "4fa3d662-ec2d-4113-97fe-2aa5ac73acf5",
                "status": "ok",
                "payload": {},
                "errors": []
              }
            }
            """;

        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<IpcStreamFrame>(Json, IpcJsonSerializerOptions.Default));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":null,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":123,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":"","message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":"lowercase_code","message":"bad","opId":null}]}""")]
    public void IpcResponse_WhenErrorCodeJsonIsInvalid_Throws (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CorrelatedEnvelopes_WhenRequestIdIsEmpty_RejectConstruction ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new { });

        Assert.Throws<ArgumentException>(() => new IpcRequest(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            "token",
            "execute",
            payload,
            ContractLiteralCodec.ToValue(IpcResponseMode.Single)));
        Assert.Throws<ArgumentException>(() => new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            IpcProtocol.StatusError,
            payload,
            Array.Empty<IpcError>()));
        Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            IpcStreamFrameKinds.Progress,
            "event",
            payload,
            response: null));
    }
}
