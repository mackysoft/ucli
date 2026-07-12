using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateRequestJsonParserSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOpRequestIsValid_ReturnsParsedRequest ()
    {
        var parser = new ValidateRequestJsonParser();

        var result = parser.Parse(ValidateRequestJsonParserTestSupport.CreateValidOperationRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(1, result.Request!.ProtocolVersion);
        var step = Assert.IsType<ValidateRequestStep>(Assert.Single(result.Request.Steps!));
        Assert.Equal(IpcRequestStepKind.Op, step.Kind);
        Assert.Equal("step-1", step.StepId);
        Assert.Equal(UcliPrimitiveOperationNames.SceneOpen, step.Op);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenEditRequestUsesDirectComponentSelection_ReturnsParsedRequest ()
    {
        var parser = new ValidateRequestJsonParser();
        var requestJson = ValidateRequestJsonParserTestSupport.CreateRequestWithSteps(
            """
            [
              {
                "kind": "edit",
                "id": "edit-1",
                "on": {
                  "scene": "Assets/Scenes/Main.unity"
                },
                "select": {
                  "gameObject": "Root/Spawner",
                  "component": "Game.EnemySpawner, Assembly-CSharp",
                  "cardinality": "one"
                },
                "actions": [
                  {
                    "kind": "set",
                    "values": {
                      "spawnInterval": 3.0
                    }
                  }
                ],
                "commit": "context"
              }
            ]
            """);

        var result = parser.Parse(requestJson);

        Assert.True(result.IsSuccess);
        var step = Assert.IsType<ValidateRequestStep>(Assert.Single(result.Request!.Steps!));
        Assert.Equal(IpcRequestStepKind.Edit, step.Kind);
        Assert.Equal("edit-1", step.StepId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenEditRequestUsesEnsureComponentBinding_ReturnsParsedRequest ()
    {
        var parser = new ValidateRequestJsonParser();
        var requestJson = ValidateRequestJsonParserTestSupport.CreateRequestWithSteps(
            """
            [
              {
                "kind": "edit",
                "id": "edit-1",
                "on": {
                  "scene": "Assets/Scenes/Main.unity"
                },
                "select": {
                  "gameObject": "Root/Spawner",
                  "cardinality": "one"
                },
                "actions": [
                  {
                    "kind": "ensureComponent",
                    "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                    "as": "collider"
                  },
                  {
                    "kind": "set",
                    "target": "$collider",
                    "values": {
                      "isTrigger": true
                    }
                  }
                ],
                "commit": "context"
              }
            ]
            """);

        var result = parser.Parse(requestJson);

        Assert.True(result.IsSuccess);
        var step = Assert.IsType<ValidateRequestStep>(Assert.Single(result.Request!.Steps!));
        Assert.Equal(IpcRequestStepKind.Edit, step.Kind);
        Assert.Equal("edit-1", step.StepId);
    }
}
