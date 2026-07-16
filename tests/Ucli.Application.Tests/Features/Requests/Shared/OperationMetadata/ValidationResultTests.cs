using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Tests.Features.Requests.Shared.OperationMetadata;

public sealed class ValidationResultTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void ValidationError_WhenMessageIsMissing_ThrowsArgumentException (string? message)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new ValidationError(
            UcliCoreErrorCodes.InternalError,
            message!,
            null));

        Assert.Equal("Message", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Invalid_WithEmptyErrors_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => ValidationResult.Invalid([]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Invalid_WithNullErrorItem_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => ValidationResult.Invalid([null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Invalid_SnapshotsErrors ()
    {
        var errors = new List<ValidationError>
        {
            new(UcliCoreErrorCodes.InternalError, "first", null),
        };

        var result = ValidationResult.Invalid(errors);
        errors.Add(new ValidationError(UcliCoreErrorCodes.InternalError, "second", null));

        Assert.Single(result.Errors);
    }
}
