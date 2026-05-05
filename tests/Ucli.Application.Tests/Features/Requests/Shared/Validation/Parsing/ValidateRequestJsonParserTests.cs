using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateRequestJsonParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOpRequestIsValid_ReturnsParsedRequest ()
    {
        var parser = new ValidateRequestJsonParser();
        var requestJson = $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;

        var result = parser.Parse(requestJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(1, result.Request!.ProtocolVersion);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Request.RequestId);
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
        const string requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
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
            }
            """;

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
        const string requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
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
            }
            """;

        var result = parser.Parse(requestJson);

        Assert.True(result.IsSuccess);
        var step = Assert.IsType<ValidateRequestStep>(Assert.Single(result.Request!.Steps!));
        Assert.Equal(IpcRequestStepKind.Edit, step.Kind);
        Assert.Equal("edit-1", step.StepId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenJsonIsMalformed_ReturnsInvalidArgument ()
    {
        AssertInvalidArgument("{", "invalid");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenRequestContainsUnknownTopLevelProperty_ReturnsInvalidArgument ()
    {
        var requestJson = $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ],
              "unknown": 1
            }
            """;

        AssertInvalidArgument(requestJson, "unknown");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationArgsPropertyIsMissing_ReturnsInvalidArgument ()
    {
        var requestJson = $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}"
                }
              ]
            }
            """;

        AssertInvalidArgument(requestJson, "args");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenStepsPropertyIsNotArray_ReturnsInvalidArgument ()
    {
        const string requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": {}
            }
            """;

        AssertInvalidArgument(requestJson, "steps");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationArgsPropertyIsNotObject_ReturnsInvalidArgument ()
    {
        var requestJson = $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
                  "args": []
                }
              ]
            }
            """;

        AssertInvalidArgument(requestJson, "args");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationContainsUnknownProperty_ReturnsInvalidArgument ()
    {
        var requestJson = CreateWrappedOpRequest(
            """
                  "unknown": 1
            """);

        AssertInvalidArgument(requestJson, "unknown");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationIdContainsOuterWhitespace_ReturnsInvalidArgument ()
    {
        var requestJson = $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": " step-1 ",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;

        AssertInvalidArgument(requestJson, "id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationNameContainsOuterWhitespace_ReturnsInvalidArgument ()
    {
        const string requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": " ucli.scene.open ",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;

        AssertInvalidArgument(requestJson, "op");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationAliasContractIsInvalid_ReturnsInvalidArgument ()
    {
        var requestJson = CreateWrappedOpRequest(
            """
                  "as": 123
            """);

        AssertInvalidArgument(requestJson, "as");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationExpectationContractIsInvalid_ReturnsInvalidArgument ()
    {
        var requestJson = CreateWrappedOpRequest(
            """
                  "expect": {
                    "count": 1,
                    "min": 0
                  }
            """);

        AssertInvalidArgument(requestJson, "expect");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenProtocolVersionTypeIsInvalid_ReturnsInvalidArgument ()
    {
        const string requestJson = """
            {
              "protocolVersion": "1",
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": []
            }
            """;

        AssertInvalidArgument(requestJson, "protocolVersion", "integer");
    }

    private static string CreateWrappedOpRequest (string trailingStepPropertyBlock)
    {
        return $$"""
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  },
            {{trailingStepPropertyBlock}}
                }
              ]
            }
            """;
    }

    private static void AssertInvalidArgument (
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
