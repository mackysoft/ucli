using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribeCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Get_WhenOperationHasNoResult_ReturnsNoResultContract ()
    {
        var describe = UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.SceneOpen);

        Assert.NotNull(describe.ResultContract);
        Assert.False(describe.ResultContract!.Emitted);
        Assert.Equal(nameof(UcliNoResult), describe.ResultContract.ResultType);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Get_WhenOperationEmitsResult_ReturnsResultContract ()
    {
        var describe = UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.AssetsFind);

        Assert.NotNull(describe.ResultContract);
        Assert.True(describe.ResultContract!.Emitted);
        Assert.Equal("AssetsFindResult", describe.ResultContract.ResultType);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Get_WhenOperationHasReferenceVariants_ReturnsArgsPathsAndConstraints ()
    {
        var describe = UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.Resolve);

        var input = Assert.Single(describe.Inputs!);
        Assert.NotNull(input.Variants);
        var variant = Assert.Single(input.Variants!, candidate => candidate.Name == "byGlobalObjectId");
        Assert.Contains("$.globalObjectId", variant.ArgsPaths!);
        Assert.Contains(variant.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.GlobalObjectId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Get_WhenOperationHasAssurance_ReturnsStructuredFields ()
    {
        var describe = UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.PrefabOpen);

        Assert.NotNull(describe.Assurance);
        Assert.Contains("opensPrefabStage", describe.Assurance!.SideEffects!);
        Assert.False(describe.Assurance.MayDirty);
        Assert.False(describe.Assurance.MayPersist);
        Assert.Contains(IpcExecuteTouchedResourceKindNames.Prefab, describe.Assurance.TouchedKinds!);
        Assert.Equal(UcliOperationPlanModeValues.MayCreatePreviewState, describe.Assurance.PlanMode);
    }
}
