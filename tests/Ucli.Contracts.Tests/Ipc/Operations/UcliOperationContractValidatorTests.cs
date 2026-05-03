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

    private sealed record RequiredStringArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required string.")]
        string? Name);

    private sealed record RequiredArrayArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required array.")]
        IReadOnlyList<string> Items);
}
