using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcEnvelopeContractSerializationTests
{
    private const int RequestDeadlineRemainingMilliseconds = 1234;

    private static readonly DateTimeOffset RequestDeadlineUtc = new(
        2030,
        1,
        2,
        3,
        4,
        5,
        TimeSpan.Zero);

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(IpcRequestEnvelope.ProtocolVersion))]
    [InlineData(nameof(IpcRequestEnvelope.RequestId))]
    [InlineData(nameof(IpcRequestEnvelope.SessionToken))]
    [InlineData(nameof(IpcRequestEnvelope.Method))]
    [InlineData(nameof(IpcRequestEnvelope.Payload))]
    [InlineData(nameof(IpcRequestEnvelope.RequestDeadlineUtc))]
    [InlineData(nameof(IpcRequestEnvelope.RequestDeadlineRemainingMilliseconds))]
    [InlineData(nameof(IpcRequestEnvelope.ResponseMode))]
    public void IpcRequest_EnvelopePropertiesAreConstructorOnly (string propertyName)
    {
        var property = typeof(IpcRequestEnvelope).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_TextRepresentations_DoNotExposeSessionToken ()
    {
        const string SessionToken = "sensitive-session-token-DO-NOT-LOG";
        var request = new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.Parse("f73322e8-d990-4f84-b47f-98e6c79d6024"),
            SessionToken,
            ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
            IpcPayloadCodec.SerializeToElement(new { }),
            ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            RequestDeadlineUtc,
            RequestDeadlineRemainingMilliseconds);

        string[] textRepresentations =
        [
            request.ToString() ?? string.Empty,
            $"Request={request}",
            new DiagnosticEnvelope(request).ToString(),
        ];

        Assert.All(
            textRepresentations,
            text => Assert.DoesNotContain(SessionToken, text, StringComparison.Ordinal));
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
        var request = new IpcRequestEnvelope(
            protocolVersion: 1,
            requestId: requestId,
            sessionToken: "token",
            method: "execute",
            payload: payload,
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: RequestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: RequestDeadlineRemainingMilliseconds);

        var json = IpcPayloadCodec.SerializeToElement(request);
        JsonAssert.For(json)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", requestId.ToString("D"))
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
            .HasString("requestDeadlineUtc", "2030-01-02T03:04:05+00:00")
            .HasInt32("requestDeadlineRemainingMilliseconds", RequestDeadlineRemainingMilliseconds)
            .HasString("responseMode", ContractLiteralCodec.ToValue(IpcResponseMode.Single))
            .HasValueKind("payload", JsonValueKind.Object);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_WhenRequestDeadlineIsDefault_RejectsConstruction ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: "token",
            method: "execute",
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: default,
            requestDeadlineRemainingMilliseconds: RequestDeadlineRemainingMilliseconds));

        Assert.Equal("requestDeadlineUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_WhenRequestDeadlineHasNonUtcOffset_RejectsConstruction ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: "token",
            method: "execute",
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(9)),
            requestDeadlineRemainingMilliseconds: RequestDeadlineRemainingMilliseconds));

        Assert.Equal("requestDeadlineUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_WhenRequestDeadlineIsPast_AcceptsConstruction ()
    {
        var request = new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: "token",
            method: "execute",
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: DateTimeOffset.UnixEpoch,
            requestDeadlineRemainingMilliseconds: RequestDeadlineRemainingMilliseconds);

        Assert.Equal(DateTimeOffset.UnixEpoch, request.RequestDeadlineUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_WhenRequestDeadlineIsMissing_RejectsDeserialization ()
    {
        const string Json = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":"execute","payload":{},"responseMode":"single","requestDeadlineRemainingMilliseconds":1234}""";

        var exception = Assert.Throws<ArgumentException>(() =>
            JsonSerializer.Deserialize<IpcRequestEnvelope>(Json, IpcJsonSerializerOptions.Default));

        Assert.Equal("requestDeadlineUtc", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":"execute","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00"}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":"execute","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":0}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":"execute","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":-1}""")]
    public void IpcRequest_WhenRequestDeadlineRemainingMillisecondsIsMissingOrNonPositive_RejectsDeserialization (string json)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            JsonSerializer.Deserialize<IpcRequestEnvelope>(json, IpcJsonSerializerOptions.Default));

        Assert.Equal("requestDeadlineRemainingMilliseconds", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":1234}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":null,"payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":1234}""")]
    public void IpcRequest_WhenMethodIsMissingOrNull_PreservesInvalidWireState (string json)
    {
        var request = JsonSerializer.Deserialize<IpcRequestEnvelope>(json, IpcJsonSerializerOptions.Default);

        Assert.NotNull(request);
        Assert.Null(request.Method);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","method":"execute","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":1234}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":null,"method":"execute","payload":{},"responseMode":"single","requestDeadlineUtc":"2030-01-02T03:04:05+00:00","requestDeadlineRemainingMilliseconds":1234}""")]
    public void IpcRequest_WhenSessionTokenIsMissingOrNull_PreservesInvalidWireState (string json)
    {
        var request = JsonSerializer.Deserialize<IpcRequestEnvelope>(json, IpcJsonSerializerOptions.Default);

        Assert.NotNull(request);
        Assert.Null(request.SessionToken);
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
            status: IpcResponseStatus.Error,
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
        const string Json = """{"protocolVersion":1,"status":"error","payload":{},"errors":[{"code":"INVALID_ARGUMENT","message":"bad request","opId":null}]}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(Json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_WhenRequestIdIsExplicitlyNull_DeserializesWithoutCorrelation ()
    {
        const string Json = """{"protocolVersion":1,"requestId":null,"status":"error","payload":{},"errors":[{"code":"INVALID_ARGUMENT","message":"bad request","opId":null}]}""";

        var response = JsonSerializer.Deserialize<IpcResponse>(Json, IpcJsonSerializerOptions.Default);

        Assert.NotNull(response);
        Assert.Null(response.RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_WhenStatusIsMissingOrUnsupported_RejectsDeserialization ()
    {
        const string MissingStatus = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","payload":{},"errors":[]}""";
        const string UnsupportedStatus = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"pending","payload":{},"errors":[]}""";

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JsonSerializer.Deserialize<IpcResponse>(MissingStatus, IpcJsonSerializerOptions.Default));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IpcResponse>(UnsupportedStatus, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_WhenStatusAndErrorsDisagree_RejectsConstruction ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new { });
        var error = new IpcError(UcliCoreErrorCodes.InvalidArgument, "invalid request", null);

        Assert.Throws<ArgumentException>(() => new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Ok,
            payload,
            [error]));
        Assert.Throws<ArgumentException>(() => new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Error,
            payload,
            Array.Empty<IpcError>()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_SnapshotsErrors ()
    {
        var originalError = new IpcError(UcliCoreErrorCodes.InvalidArgument, "invalid request", null);
        var replacementError = new IpcError(UcliCoreErrorCodes.InternalError, "replacement", null);
        IpcError[] errors = [originalError];
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Error,
            IpcPayloadCodec.SerializeToElement(new { }),
            errors);

        errors[0] = replacementError;

        Assert.Same(originalError, Assert.Single(response.Errors));
        Assert.IsNotType<IpcError[]>(response.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcError_WhenValueContractIsInvalid_RejectsConstruction ()
    {
        Assert.Throws<ArgumentNullException>(() => new IpcError(null!, "message", null));
        Assert.Throws<ArgumentNullException>(() => new IpcError(UcliCoreErrorCodes.InternalError, null!, null));
        Assert.Throws<ArgumentException>(() => new IpcError(UcliCoreErrorCodes.InternalError, " ", null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_ProgressSerializesWithCamelCaseContractFields ()
    {
        var requestId = Guid.Parse("cbd61a0f-a8db-42cf-ad3e-fb5cb558ab87");
        var runId = Guid.Parse("0a289444-5c8b-4fea-8364-eb4508d857a0");
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKind.Progress,
            TestRunProgressEventNames.RunStarted,
            IpcPayloadCodec.SerializeToElement(new TestRunStartedEntry(
                runId,
                "editmode",
                "Namespace.Tests",
                ["Assembly.Tests"],
                ["smoke"])),
            response: null);

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasInt32("protocolVersion", IpcProtocol.CurrentVersion)
            .HasString("requestId", requestId.ToString("D"))
            .HasString("kind", ContractLiteralCodec.ToValue(IpcStreamFrameKind.Progress))
            .HasString("event", TestRunProgressEventNames.RunStarted)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("response", JsonValueKind.Null);
        JsonAssert.For(json.GetProperty("payload"))
            .HasString("runId", runId.ToString("D"))
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
            IpcStreamFrameKind.Terminal,
            @event: null,
            IpcPayloadCodec.SerializeToElement(new { }),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                requestId,
                IpcResponseStatus.Ok,
                IpcPayloadCodec.SerializeToElement(new { exitCode = 0 }),
                Array.Empty<IpcError>()));

        var json = IpcPayloadCodec.SerializeToElement(frame);

        JsonAssert.For(json)
            .HasString("kind", ContractLiteralCodec.ToValue(IpcStreamFrameKind.Terminal))
            .HasValueKind("event", JsonValueKind.Null)
            .HasValueKind("response", JsonValueKind.Object);
        JsonAssert.For(json.GetProperty("response"))
            .HasString("requestId", requestId.ToString("D"))
            .HasString("status", ContractLiteralCodec.ToValue(IpcResponseStatus.Ok))
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
            IpcResponseStatus.Ok,
            payload,
            Array.Empty<IpcError>());

        Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            frameRequestId,
            IpcStreamFrameKind.Terminal,
            @event: null,
            payload,
            response));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_WhenNestedResponseProtocolVersionDiffers_RejectsConstruction ()
    {
        var requestId = Guid.Parse("30b47fea-49da-4574-b14c-15e4c9f48145");
        var payload = IpcPayloadCodec.SerializeToElement(new { });
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcResponseStatus.Ok,
            payload,
            Array.Empty<IpcError>());

        var exception = Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion + 1,
            requestId,
            IpcStreamFrameKind.Terminal,
            @event: null,
            payload,
            response));

        Assert.Equal("response", exception.ParamName);
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

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_WhenKindIsMissingOrUnsupported_RejectsDeserialization ()
    {
        const string MissingKind = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","event":"progress","payload":{},"response":null}""";
        const string UnsupportedKind = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","kind":"heartbeat","event":"progress","payload":{},"response":null}""";

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JsonSerializer.Deserialize<IpcStreamFrame>(MissingKind, IpcJsonSerializerOptions.Default));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IpcStreamFrame>(UnsupportedKind, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_WhenShapeContradictsKind_RejectsConstruction ()
    {
        var requestId = Guid.NewGuid();
        var payload = IpcPayloadCodec.SerializeToElement(new { });
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcResponseStatus.Ok,
            payload,
            Array.Empty<IpcError>());

        Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKind.Progress,
            "progress",
            payload,
            response));
        Assert.Throws<ArgumentNullException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKind.Terminal,
            @event: null,
            payload,
            response: null));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":123,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":"","message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":"lowercase_code","message":"bad","opId":null}]}""")]
    public void IpcResponse_WhenErrorCodeJsonIsInvalid_Throws (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_WhenErrorCodeJsonIsNull_RejectsAtErrorContractBoundary ()
    {
        const string Json = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","status":"error","payload":{},"errors":[{"code":null,"message":"bad","opId":null}]}""";

        Assert.Throws<ArgumentNullException>(() =>
            JsonSerializer.Deserialize<IpcResponse>(Json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CorrelatedEnvelopes_WhenRequestIdIsEmpty_RejectConstruction ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new { });

        Assert.Throws<ArgumentException>(() => new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            "token",
            "execute",
            payload,
            ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            RequestDeadlineUtc,
            RequestDeadlineRemainingMilliseconds));
        Assert.Throws<ArgumentException>(() => new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            IpcResponseStatus.Error,
            payload,
            Array.Empty<IpcError>()));
        Assert.Throws<ArgumentException>(() => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            Guid.Empty,
            IpcStreamFrameKind.Progress,
            "event",
            payload,
            response: null));
    }

    private sealed record DiagnosticEnvelope (object Value);
}
