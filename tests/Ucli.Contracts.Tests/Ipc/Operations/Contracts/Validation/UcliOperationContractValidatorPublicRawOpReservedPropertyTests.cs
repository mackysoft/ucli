using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorPublicRawOpReservedPropertyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpReservedProperties_WhenNonAliasPropertyUsesVar_ReturnsFalse ()
    {
        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            typeof(ReservedVarArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.var' uses reserved public raw-op property name 'var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpReservedProperties_WhenTopLevelAliasTypeUsesVar_ReturnsFalse ()
    {
        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            typeof(TopLevelPlanAliasVarArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.var' uses internal request-local alias type 'UcliPlanAlias'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpReservedProperties_WhenCustomAliasPropertyUsesAliasType_ReturnsFalse ()
    {
        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            typeof(CustomPlanAliasArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.alias' uses internal request-local alias type 'UcliPlanAlias'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpReservedProperties_WhenAliasReferenceTypeUsesVar_ReturnsTrue ()
    {
        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            typeof(ReferenceArgs),
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpReservedProperties_WhenCustomReferenceLikeTypeUsesVarAlias_ReturnsFalse ()
    {
        var isValid = UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
            typeof(CustomReferenceLikeArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation contract property 'args.target.var' uses internal request-local alias type 'UcliPlanAlias'.", errorMessage);
    }
}
