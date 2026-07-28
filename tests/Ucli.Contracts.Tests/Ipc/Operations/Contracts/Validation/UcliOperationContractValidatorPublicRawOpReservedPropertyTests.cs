using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorPublicRawOpReservedPropertyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenCamelCaseNamingMapsClrVarToReservedName_IsRejected ()
    {
        var generationResult = Generate<CamelCaseReservedVarArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.var' uses reserved public raw-op property name 'var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenNestedDefinitionExposesReservedName_IsRejected ()
    {
        var generationResult = Generate<NestedReservedVarArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out _);

        Assert.False(isValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenPolymorphismDiscriminatorUsesReservedName_IsRejected ()
    {
        var generationResult = Generate<ReservedDiscriminatorArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.var' uses reserved public raw-op property name 'var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenJsonPropertyNameMapsClrVarToNonReservedName_IsAccepted ()
    {
        var generationResult = Generate<RenamedReservedVarArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenJsonIgnoreRemovesClrVar_IsAccepted ()
    {
        var generationResult = Generate<IgnoredReservedVarArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenPublicResolverRemovesAliasVariant_IsAccepted ()
    {
        var generationResult = Generate<ReferenceArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedContract_WhenConverterProjectsNestedClrVarAsScalar_IsAccepted ()
    {
        var generationResult = Generate<ConvertedNestedArgs>();

        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            generationResult,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
    }

    private static UcliOperationJsonContractGenerationResult Generate<TArgs> ()
    {
        return UcliOperationJsonContractGenerator.Generate(
            "test.reserved-property",
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(TArgs)),
            resultTypeInfo: null);
    }
}
