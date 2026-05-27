using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class OperationExposureContractLiteralTests
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
    public void UcliOperationExposureContractLiteral_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("public", ContractLiteralCodec.ToValue(UcliOperationExposure.Public));
        Assert.Equal("editLoweringOnly", ContractLiteralCodec.ToValue(UcliOperationExposure.EditLoweringOnly));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("public", UcliOperationExposure.Public)]
    [InlineData("PUBLIC", UcliOperationExposure.Public)]
    [InlineData("editLoweringOnly", UcliOperationExposure.EditLoweringOnly)]
    public void UcliOperationExposureContractLiteral_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        UcliOperationExposure expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<UcliOperationExposure>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported")]
    public void UcliOperationExposureContractLiteral_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<UcliOperationExposure>(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposureContractLiteral_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<UcliOperationExposure>(null));
    }
}
