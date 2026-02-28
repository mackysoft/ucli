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
    public void IpcErrorCodes_ExposeCoreAndReadIndexConstants ()
    {
        Assert.Equal("INVALID_ARGUMENT", IpcErrorCodes.InvalidArgument);
        Assert.Equal("NOT_INITIALIZED", IpcErrorCodes.NotInitialized);
        Assert.Equal("READ_INDEX_BOOTSTRAP_FAILED", IpcErrorCodes.ReadIndexBootstrapFailed);
        Assert.Equal("READ_INDEX_FORMAT_INVALID", IpcErrorCodes.ReadIndexFormatInvalid);
        Assert.Equal("READ_INDEX_FRESH_REQUIRED", IpcErrorCodes.ReadIndexFreshRequired);
        Assert.Equal("INTERNAL_ERROR", IpcErrorCodes.InternalError);
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
        });

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("opResults", 1)
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