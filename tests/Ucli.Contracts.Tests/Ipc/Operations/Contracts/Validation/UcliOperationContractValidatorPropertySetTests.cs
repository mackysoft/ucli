using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorPropertySetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenExactlyOneExclusiveRequiredPropertySetMatches_ReturnsTrue ()
    {
        var args = new RequiredPropertySetArgs("Assets/Scenes/Main.unity", null);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RequiredPropertySetArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenExclusiveRequiredPropertySetsDoNotMatchExactlyOne_ReturnsFalse ()
    {
        var args = new RequiredPropertySetArgs("Assets/Scenes/Main.unity", "/Root");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RequiredPropertySetArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' must match exactly one exclusive required property set.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenMatchedRequiredPropertySetIncludesExtraExclusiveProperty_ReturnsFalse ()
    {
        var args = new SelectorRequiredPropertySetArgs("gid", null, "Root");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(SelectorRequiredPropertySetArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' must not mix exclusive required property sets.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenTriggerPropertyIsPresentWithoutRequiredProperties_ReturnsFalse ()
    {
        var args = new PropertyRequiresArgs(null, null, "UnityEngine.Camera");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(PropertyRequiresArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' requires properties when 'componentType' is specified.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenTriggerPropertyRequirementsArePresent_ReturnsTrue ()
    {
        var args = new PropertyRequiresArgs("Assets/Scenes/Main.unity", "Root", "UnityEngine.Transform");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(PropertyRequiresArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenResolveSelectorUsesSceneComponentSelector_ReturnsTrue ()
    {
        var args = new ResolveSelectorArgs(
            globalObjectId: null,
            assetGuid: null,
            assetPath: null,
            projectAssetPath: null,
            scene: new SceneAssetPath("Assets/Scenes/Main.unity"),
            prefab: null,
            hierarchyPath: new UnityHierarchyPath("Root"),
            componentType: new UnityComponentTypeId("UnityEngine.Transform"));

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(ResolveSelectorArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenResolveSelectorUsesAssetGuid_ReturnsTrue ()
    {
        var args = new ResolveSelectorArgs(
            globalObjectId: null,
            assetGuid: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            assetPath: null,
            projectAssetPath: null,
            scene: null,
            prefab: null,
            hierarchyPath: null,
            componentType: null);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(ResolveSelectorArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenResolveSelectorUsesPrefabComponentSelector_ReturnsFalse ()
    {
        var args = new ResolveSelectorArgs(
            globalObjectId: null,
            assetGuid: null,
            assetPath: null,
            projectAssetPath: null,
            scene: null,
            prefab: new PrefabAssetPath("Assets/Prefabs/Enemy.prefab"),
            hierarchyPath: new UnityHierarchyPath("Enemy"),
            componentType: new UnityComponentTypeId("UnityEngine.Transform"));

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(ResolveSelectorArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' requires properties when 'componentType' is specified.", errorMessage);
    }
}
