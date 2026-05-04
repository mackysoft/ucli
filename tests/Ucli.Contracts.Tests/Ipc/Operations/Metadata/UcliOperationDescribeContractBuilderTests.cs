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
        var field = Assert.Single(variant.Fields!);
        Assert.Equal("globalObjectId", field.Name);
        Assert.Equal("$.target.globalObjectId", field.ArgsPath);
        Assert.Equal("Resolved Unity GlobalObjectId.", field.Description);
        Assert.Contains(field.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.GlobalObjectId);
        var hierarchyVariant = Assert.Single(input.Variants!, candidate => candidate.Name == "bySceneHierarchyPath");
        Assert.Equal(
            "Use Scene asset path for a hierarchy selector and Unity hierarchy path inside the selected scene or prefab.",
            hierarchyVariant.Description);
        Assert.Equal(2, hierarchyVariant.Fields!.Count);
        var sceneField = Assert.Single(hierarchyVariant.Fields!, candidate => candidate.Name == "scene");
        Assert.Equal("$.target.scene", sceneField.ArgsPath);
        Assert.Equal("Scene asset path for a hierarchy selector.", sceneField.Description);
        Assert.Contains(sceneField.Constraints!, constraint =>
            constraint.Kind == UcliOperationInputConstraintKindValues.AssetExists
            && constraint.AssetKind == UcliOperationAssetKindValues.Scene);
        var hierarchyPathField = Assert.Single(hierarchyVariant.Fields!, candidate => candidate.Name == "hierarchyPath");
        Assert.Equal("$.target.hierarchyPath", hierarchyPathField.ArgsPath);
        Assert.Equal("Unity hierarchy path inside the selected scene or prefab.", hierarchyPathField.Description);
        Assert.Contains(hierarchyPathField.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.HierarchyPath);
        Assert.DoesNotContain(input.Variants!, candidate => candidate.Fields!.Any(candidateField => candidateField.ArgsPath!.EndsWith(".var", StringComparison.Ordinal)));
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
        var field = Assert.Single(variant.Fields!);
        Assert.Equal("assetGuid", field.Name);
        Assert.Equal("$.target.assetGuid", field.ArgsPath);
        Assert.Equal("Asset GUID selector.", field.Description);
        Assert.Contains(field.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.AssetGuid);
    }
}
