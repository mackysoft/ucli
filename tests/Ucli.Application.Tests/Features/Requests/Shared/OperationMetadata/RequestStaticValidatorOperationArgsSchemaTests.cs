namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorOperationArgsSchemaTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{}""")]
    [InlineData("""{"path":"Assets/Scenes/Main.unity","unexpected":true}""")]
    public async Task Validate_WhenOpStepArgsViolateRegisteredSchema_AddsOperationArgsInvalidError (string argsJson)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(
                    stepId: "step-scene-open",
                    operationName: UcliPrimitiveOperationNames.SceneOpen,
                    argsJson: argsJson),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetsFindArgsContainNoFilters_ReturnsValidForStructureOnlySchema ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-assets-find", UcliPrimitiveOperationNames.AssetsFind, new
                {
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenResolvePrefabSelectorIncludesComponentType_ReturnsValidForStructureOnlySchema ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-resolve", UcliPrimitiveOperationNames.Resolve, new
                {
                    prefab = "Assets/Prefabs/Example.prefab",
                    hierarchyPath = "Root/Child",
                    componentType = "UnityEngine.Transform, UnityEngine.CoreModule",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoDescribeUsesPrefabTargetSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-describe", UcliPrimitiveOperationNames.GoDescribe, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy",
                    },
                    depth = 1,
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetSchemaUsesTypeOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-asset-schema", UcliPrimitiveOperationNames.AssetSchema, new
                {
                    type = "MyGame.ConfigAsset, Assembly-CSharp",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCompSchemaUsesTypeOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-schema", UcliPrimitiveOperationNames.CompSchema, new
                {
                    type = "UnityEngine.Transform, UnityEngine.CoreModule",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetsFindUsesPathPrefixOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-assets-find", UcliPrimitiveOperationNames.AssetsFind, new
                {
                    pathPrefix = "Assets/Data",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoDeleteUsesPrefabTargetSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-delete", UcliPrimitiveOperationNames.GoDelete, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoReparentUsesPrefabSelectors_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-reparent", UcliPrimitiveOperationNames.GoReparent, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                    parent = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Hand",
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }
}
