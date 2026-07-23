using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

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

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationPlayModeSupport_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UcliOperationPlayModeSupport.Disallowed);
        Assert.Equal(1, (int)UcliOperationPlayModeSupport.Allowed);
        Assert.Equal(2, (int)UcliOperationPlayModeSupport.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationPlayModeSupport_HasStableContractLiterals ()
    {
        Assert.Equal("disallowed", TextVocabulary.GetText(UcliOperationPlayModeSupport.Disallowed));
        Assert.Equal("allowed", TextVocabulary.GetText(UcliOperationPlayModeSupport.Allowed));
        Assert.Equal("required", TextVocabulary.GetText(UcliOperationPlayModeSupport.Required));
    }
}
