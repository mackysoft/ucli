using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class ValidateRequestJsonParserTestSupport
{
    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";
    public const string ScenePath = "Assets/Scenes/Main.unity";

    public static string ValidOperationStepsJson => $$"""
        [
          {
            "kind": "op",
            "id": "step-1",
            "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
            "args": {
              "path": "{{ScenePath}}"
            }
          }
        ]
        """;

    public static string CreateRequestWithSteps (
        string stepsJson,
        string trailingRequestProperties = "")
    {
        return $$"""
            {
              "protocolVersion": 1,
              "requestId": "{{RequestId}}",
              "steps": {{stepsJson}}{{trailingRequestProperties}}
            }
            """;
    }

    public static string CreateValidOperationRequest (string trailingOperationProperties = "")
    {
        return CreateOperationRequest($$"""
            "kind": "op",
            "id": "step-1",
            "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
            "args": {
              "path": "{{ScenePath}}"
            }{{trailingOperationProperties}}
            """);
    }

    public static string CreateOperationRequest (string operationStepProperties)
    {
        var stepsJson = $$"""
            [
              {
            {{operationStepProperties}}
              }
            ]
            """;
        return CreateRequestWithSteps(stepsJson);
    }

    public static void AssertInvalidArgument (
        string requestJson,
        params string[] expectedFragments)
    {
        var parser = new ValidateRequestJsonParser();

        var result = parser.Parse(requestJson);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Request);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        foreach (var expectedFragment in expectedFragments)
        {
            Assert.Contains(expectedFragment, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
