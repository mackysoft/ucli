namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorRawOperationExposureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawOperationExposureIsEditLoweringOnly_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var operationName = "ucli.tests.edit-lowering-only";
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-edit-lowering-only", operationName, new
                {
                }),
            ]);
        var operations = new[]
        {
            CreateDescriptor(operationName, exposure: UcliOperationExposure.EditLoweringOnly),
        };

        var result = await validator.ValidateAsync(
            request,
            operations,
            CreateConfig(OperationPolicy.Safe, "^ucli\\.tests\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains(
            result.Errors,
            error => error.Code == UcliCoreErrorCodes.InvalidArgument
                     && error.Message.Contains("available only through edit lowering", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPublicCatalogOmitsEditLoweringPrimitiveButRawOpReferencesIt_AddsInvalidArgumentError ()
    {
        foreach (var operationName in EditLoweringOnlyPrimitiveNames)
        {
            var validator = CreateValidator();
            var request = CreateRequest(
                steps:
                [
                    CreateOpStep("step-edit-only-raw", operationName, new
                    {
                    }),
                ]);

            var result = await validator.ValidateAsync(
                request,
                Array.Empty<UcliOperationDescriptor>(),
                CreateConfig(OperationPolicy.Advanced, "^ucli\\."),
                CancellationToken.None);

            AssertContainsEditLoweringOnlyError(result, operationName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawCompSetIsEditLoweringOnly_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-set", UcliPrimitiveOperationNames.CompSet, new
                {
                    target = new
                    {
                        globalObjectId = "GlobalObjectId_V1-2-3-4-5-6",
                    },
                    sets = Array.Empty<object>(),
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.CompSet);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawGoCreateIsEditLoweringOnly_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-create", UcliPrimitiveOperationNames.GoCreate, new
                {
                    name = "GeneratedChild",
                    parent = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy",
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.GoCreate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawCompSetUsesPrefabComponentSelector_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-set", UcliPrimitiveOperationNames.CompSet, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Armature",
                        componentType = "UnityEngine.Transform, UnityEngine.CoreModule",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "m_LocalPosition.x",
                            value = 1,
                        },
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.CompSet);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawCompSetItemMissesRequiredPath_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-set", UcliPrimitiveOperationNames.CompSet, new
                {
                    target = new
                    {
                        globalObjectId = "GlobalObjectId_V1-2-3-4-5-6",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            value = 1,
                        },
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.CompSet);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawAssetSetIsEditLoweringOnly_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-asset-set", UcliPrimitiveOperationNames.AssetSet, new
                {
                    target = new
                    {
                        projectAssetPath = "ProjectSettings/TagManager.asset",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "tags.Array.size",
                            value = 1,
                        },
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.AssetSet);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawPrefabCreateIsEditLoweringOnly_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-prefab-create", UcliPrimitiveOperationNames.PrefabCreate, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                    path = "Assets/Prefabs/Generated.prefab",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.PrefabCreate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawGoCreateUsesVarSelector_AddsInvalidArgumentError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-create", UcliPrimitiveOperationNames.GoCreate, new
                {
                    name = "GeneratedChild",
                    parent = new
                    {
                        @var = "created-parent",
                    },
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        AssertContainsEditLoweringOnlyError(result, UcliPrimitiveOperationNames.GoCreate);
    }
}
