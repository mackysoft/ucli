using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcContractSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProtocol_ExposeStableLiterals ()
    {
        Assert.Equal(1, IpcProtocol.CurrentVersion);
        Assert.Equal("ok", IpcProtocol.StatusOk);
        Assert.Equal("error", IpcProtocol.StatusError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_SerializesWithCamelCaseContractFields ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            command = "status",
        }, SerializerOptions);
        var request = new IpcRequest(
            ProtocolVersion: 1,
            RequestId: "req-1",
            SessionToken: "token",
            Method: "execute",
            Payload: payload);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(request, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", "req-1")
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
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
            ProtocolVersion: 1,
            RequestId: "req-2",
            Status: "error",
            Payload: payload,
            Errors:
            [
                new IpcError("COMMAND_NOT_IMPLEMENTED", "Not implemented", null),
            ]);

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<IpcResponse>(json, SerializerOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Status, roundTrip.Status);
        Assert.Single(roundTrip.Errors);
        Assert.Equal("COMMAND_NOT_IMPLEMENTED", roundTrip.Errors[0].Code);
        Assert.Equal("Not implemented", roundTrip.Errors[0].Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcErrorCodes_ExposeCoreAndReadIndexConstants ()
    {
        Assert.Equal("INVALID_ARGUMENT", IpcErrorCodes.InvalidArgument);
        Assert.Equal("NOT_INITIALIZED", IpcErrorCodes.NotInitialized);
        Assert.Equal("SESSION_TOKEN_REQUIRED", IpcErrorCodes.SessionTokenRequired);
        Assert.Equal("SESSION_TOKEN_INVALID", IpcErrorCodes.SessionTokenInvalid);
        Assert.Equal("READ_INDEX_BOOTSTRAP_FAILED", IpcErrorCodes.ReadIndexBootstrapFailed);
        Assert.Equal("READ_INDEX_FORMAT_INVALID", IpcErrorCodes.ReadIndexFormatInvalid);
        Assert.Equal("READ_INDEX_FRESH_REQUIRED", IpcErrorCodes.ReadIndexFreshRequired);
        Assert.Equal("PLAN_TOKEN_REQUIRED", IpcErrorCodes.PlanTokenRequired);
        Assert.Equal("PLAN_TOKEN_INVALID", IpcErrorCodes.PlanTokenInvalid);
        Assert.Equal("PLAN_TOKEN_EXPIRED", IpcErrorCodes.PlanTokenExpired);
        Assert.Equal("PLAN_TOKEN_REQUEST_MISMATCH", IpcErrorCodes.PlanTokenRequestMismatch);
        Assert.Equal("STATE_CHANGED_SINCE_PLAN", IpcErrorCodes.StateChangedSincePlan);
        Assert.Equal("REQUEST_ID_CONFLICT", IpcErrorCodes.RequestIdConflict);
        Assert.Equal("INTERNAL_ERROR", IpcErrorCodes.InternalError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteRequest_SerializesPlanTokenOnlyWhenSpecified ()
    {
        var requestWithToken = new IpcExecuteRequest(
            Command: IpcExecuteCommandNames.Call,
            Arguments: JsonSerializer.SerializeToElement(new { protocolVersion = 1, requestId = "req-1", ops = Array.Empty<object>() }))
        {
            PlanToken = "token-value",
        };

        var withTokenJson = JsonSerializer.SerializeToElement(requestWithToken, SerializerOptions);
        Assert.True(withTokenJson.TryGetProperty("planToken", out var planTokenElement));
        Assert.Equal("token-value", planTokenElement.GetString());

        var requestWithoutToken = new IpcExecuteRequest(
            Command: IpcExecuteCommandNames.Plan,
            Arguments: JsonSerializer.SerializeToElement(new { protocolVersion = 1, requestId = "req-1", ops = Array.Empty<object>() }));
        var withoutTokenJson = JsonSerializer.SerializeToElement(requestWithoutToken, SerializerOptions);
        Assert.False(withoutTokenJson.TryGetProperty("planToken", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcMethodNames_ExposeExpectedMethodLiterals ()
    {
        Assert.Equal("ping", IpcMethodNames.Ping);
        Assert.Equal("execute", IpcMethodNames.Execute);
        Assert.Equal("test.run", IpcMethodNames.TestRun);
        Assert.Equal("shutdown", IpcMethodNames.Shutdown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcShutdownContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcShutdownRequest(RequestedBy: "ucli-daemon-stop");
        var responsePayload = new IpcShutdownResponse(Accepted: true, Message: "Shutdown accepted.");

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("requestedBy", "ucli-daemon-stop");
        JsonAssert.For(responseDocument.RootElement)
            .HasBoolean("accepted", true)
            .HasString("message", "Shutdown accepted.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcTestRunRequest(
            TestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: null,
            TestCategories: Array.Empty<string>(),
            AssemblyNames: Array.Empty<string>(),
            TestSettingsPath: null,
            ResultsXmlPath: "/tmp/results.xml",
            EditorLogPath: "/tmp/editor.log");
        var responsePayload = new IpcTestRunResponse(ExitCode: 2);

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("testPlatform", "editmode")
            .IsNull("buildTarget")
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .IsNull("testSettingsPath")
            .HasString("resultsXmlPath", "/tmp/results.xml")
            .HasString("editorLogPath", "/tmp/editor.log");
        JsonAssert.For(responseDocument.RootElement)
            .HasInt32("exitCode", 2);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesWithOpResultsContract ()
    {
        var response = new IpcExecuteResponse(new[]
        {
            new IpcExecuteOperationResult(
                OpId: "op-1",
                Op: "ucli.resolve",
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: new[]
                {
                    new IpcExecuteTouchedResource(
                        Kind: IpcExecuteTouchedResourceKindNames.Scene,
                        Path: "Assets/Scenes/Main.unity",
                        Guid: "11111111111111111111111111111111"),
                }),
        })
        {
            PlanToken = "issued-token",
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("opResults", 1)
            .HasString("planToken", "issued-token")
            .HasProperty("opResults", 0, opResult => opResult
                .HasString("opId", "op-1")
                .HasString("op", "ucli.resolve")
                .HasString("phase", IpcExecuteOperationPhaseNames.Call)
                .HasBoolean("applied", true)
                .HasBoolean("changed", true)
                .HasArrayLength("touched", 1)
                .HasProperty("touched", 0, touched => touched
                    .HasString("kind", IpcExecuteTouchedResourceKindNames.Scene)
                    .HasString("path", "Assets/Scenes/Main.unity")
                    .HasString("guid", "11111111111111111111111111111111")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_OmitsPlanTokenWhenNull ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>());

        var jsonElement = JsonSerializer.SerializeToElement(response, SerializerOptions);
        Assert.False(jsonElement.TryGetProperty("planToken", out _));
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

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationPhaseNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("validate", IpcExecuteOperationPhaseNames.Validate);
        Assert.Equal("plan", IpcExecuteOperationPhaseNames.Plan);
        Assert.Equal("call", IpcExecuteOperationPhaseNames.Call);
        Assert.Equal("skipped", IpcExecuteOperationPhaseNames.Skipped);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteTouchedResourceKindNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("scene", IpcExecuteTouchedResourceKindNames.Scene);
        Assert.Equal("prefab", IpcExecuteTouchedResourceKindNames.Prefab);
        Assert.Equal("asset", IpcExecuteTouchedResourceKindNames.Asset);
        Assert.Equal("projectSettings", IpcExecuteTouchedResourceKindNames.ProjectSettings);
    }

}