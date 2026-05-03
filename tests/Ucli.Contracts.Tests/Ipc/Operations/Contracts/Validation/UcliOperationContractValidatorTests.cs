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

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenExactlyOneRequiredPropertyAlternativeMatches_ReturnsTrue ()
    {
        var args = new AlternativeArgs("Assets/Scenes/Main.unity", null);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(AlternativeArgs), out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRequiredPropertyAlternativesDoNotMatchExactlyOne_ReturnsFalse ()
    {
        var args = new AlternativeArgs("Assets/Scenes/Main.unity", "/Root");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(AlternativeArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' must match exactly one required-property alternative.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenTriggerPropertyIsPresentWithoutDependency_ReturnsFalse ()
    {
        var args = new DependencyArgs(null, null, "UnityEngine.Camera");

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(DependencyArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args' requires dependent properties when 'componentType' is specified.", errorMessage);
    }

    private sealed record RequiredStringArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required string.")]
        string? Name);

    private sealed record RequiredArrayArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required array.")]
        IReadOnlyList<string> Items);

    [UcliRequiredPropertyAlternative("scene")]
    [UcliRequiredPropertyAlternative("parent")]
    private sealed record AlternativeArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Parent hierarchy path.")]
        string? Parent);

    [UcliPropertyDependency("componentType", "scene", "hierarchyPath")]
    private sealed record DependencyArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Hierarchy path.")]
        string? HierarchyPath,

        [property: UcliDescription("Component type.")]
        string? ComponentType);
}
