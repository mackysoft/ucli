using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorPresenceTests
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
    public void TryValidate_WhenNonEmptyArrayIsEmpty_ReturnsFalse ()
    {
        var args = new NonEmptyArrayArgs(Array.Empty<string>());

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(NonEmptyArrayArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.items' must not be empty.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenNonEmptyJsonObjectIsEmpty_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse("{}");
        var args = new NonEmptyJsonObjectArgs(document.RootElement.Clone());

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(NonEmptyJsonObjectArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.value' must not be empty.", errorMessage);
    }
}
