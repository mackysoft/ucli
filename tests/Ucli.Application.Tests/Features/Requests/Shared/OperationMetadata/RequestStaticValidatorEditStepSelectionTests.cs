namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorEditStepSelectionTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"scene":"Assets/Scenes/Main.unity"}""")]
    [InlineData("""{"unknown":"value"}""")]
    public async Task Validate_WhenEditSelectFromArgsAreInvalid_AddsEditStepInvalidError (string fromArgsJson)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-query",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-query",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "from": {
                          "op": "__SCENE_QUERY_OP__",
                          "args": __ARGS__
                        },
                        "cardinality": "all"
                      },
                      "actions": [
                        {
                          "kind": "delete"
                        }
                      ],
                      "commit": "context"
                    }
                    """
                        .Replace("__ARGS__", fromArgsJson, StringComparison.Ordinal)
                        .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal)),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenDirectEditSelectionUsesFirst_AddsEditStepInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-direct-first",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-direct-first",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "gameObject": "Root",
                        "cardinality": "first"
                      },
                      "actions": [
                        {
                          "kind": "delete"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesSceneQuerySelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-query",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-query",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "from": {
                          "op": "__SCENE_QUERY_OP__",
                          "args": {
                            "pathPrefix": "Root/Enemies",
                            "componentType": "Game.EnemySpawner, Assembly-CSharp"
                          }
                        },
                        "cardinality": "all"
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
                    """
                        .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal)),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesSceneQueryFirstSelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-query-first",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-query-first",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "from": {
                          "op": "__SCENE_QUERY_OP__",
                          "args": {
                            "pathPrefix": "Root/Enemies"
                          }
                        },
                        "cardinality": "first"
                      },
                      "actions": [
                        {
                          "kind": "delete"
                        }
                      ],
                      "commit": "none"
                    }
                    """
                        .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal)),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestTargetsDirectComponentSelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-1",
                    """
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
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }
}
