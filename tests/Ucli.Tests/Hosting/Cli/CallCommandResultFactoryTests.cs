using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;

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
                ApplicationFailure.InternalError("Call failed."),
            ],
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    OpResults: [],
                    PlanToken: null),
                ReadPostcondition: null)));

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
                ApplicationFailure.InternalError("Call failed."),
            ]));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        Assert.False(json.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenReadPostconditionExists_EmitsTopLevelPayloadOnly ()
    {
        var readPostcondition = new OperationExecutionReadPostcondition(
        [
            new OperationExecutionReadPostconditionRequirement(
                Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T01:02:03+00:00"))
            {
                ScenePath = "Assets/Scenes/Main.unity",
            },
        ]);
        var result = CallCommandResultFactory.Create(CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    OpResults: [],
                    PlanToken: "plan-token-1"),
                ReadPostcondition: readPostcondition),
            "uCLI call completed."));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasProperty("readPostcondition", readPostconditionElement => readPostconditionElement
                .HasArrayLength("requirements", 1)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite)
                    .HasString("scenePath", "Assets/Scenes/Main.unity")));
        Assert.False(payload.GetProperty("plan").TryGetProperty("readPostcondition", out _));
    }
}
