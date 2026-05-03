using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRequiredStringIsEmpty_TreatsValueAsPresent ()
    {
        var args = new RequiredStringArgs(string.Empty);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RequiredStringArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRequiredArrayIsEmpty_TreatsValueAsPresent ()
    {
        var args = new RequiredArrayArgs(Array.Empty<string>());

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RequiredArrayArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRequiredStringIsNull_ReturnsFalse ()
    {
        var args = new RequiredStringArgs(null);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RequiredStringArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' requires property 'name'.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryValidate_WhenNonEmptyStringIsBlank_ReturnsFalse (string name)
    {
        var args = new NonEmptyStringArgs(name);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(NonEmptyStringArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.name' must not be empty.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenNonEmptySemanticStringIsBlank_ReturnsFalse ()
    {
        var args = new NonEmptySemanticStringArgs(new UnityHierarchyPathPrefix("   "));

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(NonEmptySemanticStringArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.pathPrefix' must not be empty.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenNonEmptyArrayIsEmpty_ReturnsFalse ()
    {
        var args = new NonEmptyArrayArgs(Array.Empty<string>());

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(NonEmptyArrayArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.items' must not be empty.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRangeValueIsBelowMinimum_ReturnsFalse ()
    {
        var args = new RangeArgs(-1);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RangeArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.depth' must be greater than or equal to 0.", errorMessage);
    }

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

    private sealed record RequiredStringArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required string.")]
        string? Name);

    private sealed record RequiredArrayArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required array.")]
        IReadOnlyList<string> Items);

    private sealed record NonEmptyStringArgs (
        [property: UcliDescription("Name.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
        string? Name);

    private sealed record NonEmptySemanticStringArgs (
        [property: UcliDescription("Path prefix.")]
        UnityHierarchyPathPrefix? PathPrefix);

    private sealed record NonEmptyArrayArgs (
        [property: UcliDescription("Items.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
        IReadOnlyList<string> Items);

    private sealed record RangeArgs (
        [property: UcliDescription("Depth.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
        int Depth);

    [UcliExclusiveRequiredPropertySet("scene")]
    [UcliExclusiveRequiredPropertySet("parent")]
    private sealed record RequiredPropertySetArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Parent hierarchy path.")]
        string? Parent);

    [UcliExclusiveRequiredPropertySet("globalObjectId")]
    [UcliExclusiveRequiredPropertySet("scene", "hierarchyPath")]
    private sealed record SelectorRequiredPropertySetArgs (
        [property: UcliDescription("GlobalObjectId.")]
        string? GlobalObjectId,

        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Hierarchy path.")]
        string? HierarchyPath);

    [UcliPropertyRequires("componentType", "scene", "hierarchyPath")]
    private sealed record PropertyRequiresArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Hierarchy path.")]
        string? HierarchyPath,

        [property: UcliDescription("Component type.")]
        string? ComponentType);
}
