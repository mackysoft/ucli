namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorEditStepActionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesEnsureAndSetBindings ()
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
                    """),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabEditContainsCreatePrefab_AddsEditStepInvalidError ()
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
                        "kind": "prefab",
                        "path": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "kind": "gameObject",
                        "path": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createPrefab",
                          "path": "Assets/Generated/EnemyChild.prefab"
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
}
