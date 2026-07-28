namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorEditStepSelectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesSceneQuerySelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepIndex: 0,
                    """
                    {
                      "kind": "edit",
                      "on": {
                        "kind": "scene",
                        "path": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "kind": "from",
                        "op": "__SCENE_QUERY_OP__",
                        "args": {
                          "pathPrefix": "Root/Enemies",
                          "componentType": "Game.EnemySpawner, Assembly-CSharp"
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
                    stepIndex: 0,
                    """
                    {
                      "kind": "edit",
                      "on": {
                        "kind": "scene",
                        "path": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "kind": "from",
                        "op": "__SCENE_QUERY_OP__",
                        "args": {
                          "pathPrefix": "Root/Enemies"
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
                    stepIndex: 0,
                    """
                    {
                      "kind": "edit",
                      "on": {
                        "kind": "scene",
                        "path": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "kind": "gameObject",
                        "path": "Root/Spawner",
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
