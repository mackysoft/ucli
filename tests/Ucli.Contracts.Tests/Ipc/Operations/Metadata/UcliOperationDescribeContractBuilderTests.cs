using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribeContractBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenOperationHasNoResult_ReturnsNoResultContract ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

        Assert.NotNull(describe.ResultContract);
        Assert.False(describe.ResultContract!.Emitted);
        Assert.Equal(nameof(UcliNoResult), describe.ResultContract.ResultType);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenOperationEmitsResult_UsesResultTypeDescription ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<AssetsFindArgs, AssetsFindResult>(
            "Finds project assets by type, path prefix, or name substring.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

        Assert.NotNull(describe.ResultContract);
        Assert.True(describe.ResultContract!.Emitted);
        Assert.Equal(nameof(AssetsFindResult), describe.ResultContract.ResultType);
        Assert.Equal("Assets find operation result.", describe.ResultContract.Description);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenInputHasAttributes_ReturnsDescriptionsAndConstraints ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

        var input = Assert.Single(describe.Inputs!);
        Assert.Equal("path", input.Name);
        Assert.Equal("Project-relative path to an existing Unity scene asset.", input.Description);
        Assert.Equal("string", input.ValueType);
        Assert.Contains(input.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.NonEmpty);
        Assert.Contains(input.Constraints!, constraint =>
            constraint.Kind == UcliOperationInputConstraintKindValues.AssetExists
            && constraint.AssetKind == UcliOperationAssetKindValues.Scene);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenInputHasReferenceType_ReturnsReferenceVariants ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

        var input = Assert.Single(describe.Inputs!, candidate => candidate.Name == "target");
        Assert.NotNull(input.Variants);
        var variant = Assert.Single(input.Variants!, candidate => candidate.Name == "byGlobalObjectId");
        Assert.Contains("$.target.globalObjectId", variant.ArgsPaths!);
        Assert.Contains(variant.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.GlobalObjectId);
        Assert.DoesNotContain(input.Variants!, candidate => candidate.ArgsPaths!.Any(path => path.EndsWith(".var", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenReferenceInputHasAssetGuidVariant_ReturnsAssetGuidConstraint ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<AssetSchemaArgs, UcliNoResult>(
            "Returns serialized property schema for a Unity asset.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

        var input = Assert.Single(describe.Inputs!, candidate => candidate.Name == "target");
        var variant = Assert.Single(input.Variants!, candidate => candidate.Name == "byAssetGuid");
        Assert.Contains("$.target.assetGuid", variant.ArgsPaths!);
        Assert.Contains(variant.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.AssetGuid);
    }
}
