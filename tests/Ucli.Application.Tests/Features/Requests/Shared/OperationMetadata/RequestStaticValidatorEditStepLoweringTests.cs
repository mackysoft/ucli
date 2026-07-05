namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorEditStepLoweringTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenOperationsAreNotProvided_StillValidatesEditLowering ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-invalid",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-invalid",
                      "actions": []
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            RequestStaticValidationCatalog.Unavailable,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("applyPrefabOverrides")]
    [InlineData("revertPrefabOverrides")]
    public async Task Validate_WhenPublicCatalogOmitsPrefabOverrideEditLoweringPrimitive_RemainsValid (
        string prefabOverrideAction)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-override",
                    $$"""
                    {
                      "kind": "edit",
                      "id": "edit-prefab-override",
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
                        },
                        {
                          "kind": "{{prefabOverrideAction}}",
                          "targetAssetPath": "Assets/Prefabs/Enemy.prefab"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            Array.Empty<UcliOperationDescriptor>(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }
}
