using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithRawPayload_PreservesMethodAndPayload ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            value = 42,
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Raw("custom.method", payload));

        Assert.Equal("custom.method", request.Method);
        Assert.Equal(payload.GetRawText(), request.Payload.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPing_CreatesPingPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Ping(
            "test-client",
            FailFast: true));

        Assert.Equal(IpcMethodNames.Ping, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
        Assert.Equal("test-client", payload.ClientVersion);
        Assert.True(payload.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteJson_CreatesExecutePayload ()
    {
        var executeArguments = JsonSerializer.SerializeToElement(new
        {
            requestId = "request-1",
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Plan,
            executeArguments,
            FailFast: true,
            AllowDangerous: true,
            PlanToken: "plan-token"));

        Assert.Equal(IpcMethodNames.Execute, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest payload, out _));
        Assert.Equal(UcliCommandIds.Plan.Name, payload.Command);
        Assert.True(payload.FailFast);
        Assert.True(payload.AllowDangerous);
        Assert.Equal("plan-token", payload.PlanToken);
        Assert.Equal(executeArguments.GetRawText(), payload.Arguments.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteOperation_CreatesSingleOperationExecutePayload ()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            path = "Assets/Test.prefab",
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.ExecuteOperation(
            UcliCommandIds.Call,
            "request-1",
            "op-1",
            "asset.create",
            args,
            FailFast: false,
            AllowDangerous: true,
            PlanToken: "plan-token"));

        Assert.Equal(IpcMethodNames.Execute, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest payload, out _));
        Assert.Equal(UcliCommandIds.Call.Name, payload.Command);
        Assert.False(payload.FailFast);
        Assert.True(payload.AllowDangerous);
        Assert.Equal("plan-token", payload.PlanToken);
        Assert.Equal(IpcProtocol.CurrentVersion, payload.Arguments.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("request-1", payload.Arguments.GetProperty("requestId").GetString());
        var step = payload.Arguments.GetProperty("steps")[0];
        Assert.Equal("op", step.GetProperty("kind").GetString());
        Assert.Equal("op-1", step.GetProperty("id").GetString());
        Assert.Equal("asset.create", step.GetProperty("op").GetString());
        Assert.Equal(args.GetRawText(), step.GetProperty("args").GetRawText());
    }
}
