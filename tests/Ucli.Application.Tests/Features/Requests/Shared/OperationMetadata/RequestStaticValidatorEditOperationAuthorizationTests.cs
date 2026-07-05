namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorEditOperationAuthorizationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenSceneCreateAssetOnlyEditDisallowsSceneOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-scene-create-asset",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-scene-create-asset",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "gameObject": "Root/Spawner",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createAsset",
                          "path": "Assets/Generated/SpawnConfig.asset",
                          "type": "Game.SpawnConfig, Assembly-CSharp"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.create$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabCreateAssetOnlyEditDisallowsPrefabOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-create-asset",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-prefab-create-asset",
                      "on": {
                        "prefab": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "gameObject": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createAsset",
                          "path": "Assets/Generated/EnemyConfig.asset",
                          "type": "Game.EnemyConfig, Assembly-CSharp"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.create$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenSceneMutationEditDisallowsSceneOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-scene-ensure",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-scene-ensure",
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
                          "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenEditLoweringReferencesEditLoweringOnlyOperation_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateSceneEnsureEditStep("edit-comp-ensure"),
            ]);
        var operations = new[]
        {
            CreateDescriptor(
                UcliPrimitiveOperationNames.CompEnsure,
                policy: OperationPolicy.Advanced,
                exposure: UcliOperationExposure.EditLoweringOnly),
        };

        var result = await validator.ValidateAsync(
            request,
            operations,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPublicCatalogOmitsEditLoweringPrimitiveAndPolicyDisallowsIt_AddsOperationNotAllowedError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateSceneEnsureEditStep("edit-comp-ensure"),
            ]);

        var result = await validator.ValidateAsync(
            request,
            Array.Empty<UcliOperationDescriptor>(),
            CreateConfig(OperationPolicy.Safe, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == OperationAuthorizationErrorCodes.OperationNotAllowed
                     && error.Message.Contains("Edit step 'edit-comp-ensure' requires operation 'ucli.comp.ensure'.", StringComparison.Ordinal)
                     && error.Message.Contains("operationPolicy", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPublicCatalogOmitsEditLoweringPrimitiveAndAllowlistExcludesIt_AddsOperationNotAllowedError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateSceneEnsureEditStep("edit-comp-ensure"),
            ]);

        var result = await validator.ValidateAsync(
            request,
            Array.Empty<UcliOperationDescriptor>(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.create$"),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == OperationAuthorizationErrorCodes.OperationNotAllowed
                     && error.Message.Contains("Edit step 'edit-comp-ensure' requires operation 'ucli.comp.ensure'.", StringComparison.Ordinal)
                     && error.Message.Contains("operationAllowlist", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("asset")]
    [InlineData("project")]
    public async Task Validate_WhenAllowPlayModeAssetBackedCommitUsesTargetLimitedAssetSave_DoesNotRequireProjectSave (
        string contextKind)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            allowPlayMode: true,
            steps:
            [
                CreateAssetSetEditStep("edit-asset-save", contextKind),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.(set|save)$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("asset")]
    [InlineData("project")]
    public async Task Validate_WhenAssetBackedCommitRunsOutsidePlayMode_RequiresProjectSave (
        string contextKind)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateAssetSetEditStep("edit-project-save", contextKind),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.(set|save)$"),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == OperationAuthorizationErrorCodes.OperationNotAllowed
                     && error.Message.Contains(UcliPrimitiveOperationNames.ProjectSave, StringComparison.Ordinal)
                     && error.Message.Contains("operationAllowlist", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabMutationEditDisallowsPrefabOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-ensure",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-prefab-ensure",
                      "on": {
                        "prefab": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "gameObject": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "ensureComponent",
                          "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }
}
