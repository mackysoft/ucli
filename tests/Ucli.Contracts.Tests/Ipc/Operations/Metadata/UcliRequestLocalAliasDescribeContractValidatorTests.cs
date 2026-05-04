using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliRequestLocalAliasDescribeContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenInputArgsPathUsesRequestLocalAlias_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                argsPath: "$.target.var"),
        };

        var isValid = UcliRequestLocalAliasDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Describe contract input 'target' must not expose request-local alias args path '$.target.var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldArgsPathUsesRequestLocalAlias_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byAlias",
                        "Use request-local alias.",
                        new[]
                        {
                            new UcliOperationInputVariantFieldContract(
                                "var",
                                "$.target.var",
                                "Request-local alias.",
                                Array.Empty<UcliOperationInputConstraintContract>()),
                        }),
                }),
        };

        var isValid = UcliRequestLocalAliasDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Describe contract input 'target' variant 'byAlias' must not expose request-local alias args path '$.target.var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldIsNull_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byAlias",
                        "Use request-local alias.",
                        new UcliOperationInputVariantFieldContract[]
                        {
                            null!,
                        }),
                }),
        };

        var isValid = UcliRequestLocalAliasDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Describe contract input 'target' variant 'byAlias' field at index 0 must not be null.", errorMessage);
    }
}
