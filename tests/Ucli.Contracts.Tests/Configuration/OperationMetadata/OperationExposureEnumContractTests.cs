using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class OperationExposureEnumContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationPlayModeSupport_HasStableContractLiterals ()
    {
        Assert.Equal("disallowed", TextVocabulary.GetText(UcliOperationPlayModeSupport.Disallowed));
        Assert.Equal("allowed", TextVocabulary.GetText(UcliOperationPlayModeSupport.Allowed));
        Assert.Equal("required", TextVocabulary.GetText(UcliOperationPlayModeSupport.Required));
    }
}
