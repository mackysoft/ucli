using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Hosting.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandResultFactoryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPlanTokenIsMissing_OmitsPlanTokenFromNestedPayload ()
    {
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Call failed.",
            [
                new IpcError(IpcErrorCodes.InternalError, "Call failed.", null),
            ],
            (int)CliExitCode.ToolError,
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    OpResults: [],
                    PlanToken: null))));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        var payload = json.RootElement.GetProperty("payload");
        Assert.True(payload.TryGetProperty("plan", out var planElement));
        Assert.False(planElement.TryGetProperty("planToken", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenOutputIsMissing_UsesEmptyPayload ()
    {
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Call failed.",
            [
                new IpcError(IpcErrorCodes.InternalError, "Call failed.", null),
            ],
            (int)CliExitCode.ToolError,
            output: null));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        Assert.False(json.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }
}