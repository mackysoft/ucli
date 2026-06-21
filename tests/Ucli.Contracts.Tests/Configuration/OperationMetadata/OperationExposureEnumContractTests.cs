using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class OperationExposureEnumContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposure_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UcliOperationExposure.Public);
        Assert.Equal(1, (int)UcliOperationExposure.EditLoweringOnly);
    }
}
