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
                CreateSceneEnsureEditStep(0),
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
                CreateSceneEnsureEditStep(0),
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
                     && error.Message.Contains("Edit step requires operation 'ucli.comp.ensure'.", StringComparison.Ordinal)
                     && error.InstancePath == "/steps/0"
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
                CreateSceneEnsureEditStep(0),
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
                     && error.Message.Contains("Edit step requires operation 'ucli.comp.ensure'.", StringComparison.Ordinal)
                     && error.InstancePath == "/steps/0"
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
                CreateAssetSetEditStep(0, contextKind),
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
                CreateAssetSetEditStep(0, contextKind),
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
