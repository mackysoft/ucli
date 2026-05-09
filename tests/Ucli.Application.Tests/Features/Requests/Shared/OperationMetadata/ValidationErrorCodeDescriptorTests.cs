namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

public sealed class ValidationErrorCodeDescriptorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptors_CoverOwnedValidationCodes ()
    {
        var expectedCodes = ValidationErrorCodes.All
            .OrderBy(static code => code.Value, StringComparer.Ordinal)
            .ToArray();
        var actualCodes = ValidationErrorCodeDescriptors.All
            .Select(static descriptor => descriptor.Code)
            .OrderBy(static code => code.Value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCodes, actualCodes);
    }
}
