using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class OperationExposureCodecContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposure_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UcliOperationExposure.Public);
        Assert.Equal(1, (int)UcliOperationExposure.EditLoweringOnly);
        Assert.Equal(2, (int)UcliOperationExposure.Internal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposureCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(UcliOperationExposureValues.Public, UcliOperationExposureCodec.ToValue(UcliOperationExposure.Public));
        Assert.Equal(UcliOperationExposureValues.EditLoweringOnly, UcliOperationExposureCodec.ToValue(UcliOperationExposure.EditLoweringOnly));
        Assert.Equal(UcliOperationExposureValues.Internal, UcliOperationExposureCodec.ToValue(UcliOperationExposure.Internal));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("public", UcliOperationExposure.Public)]
    [InlineData("PUBLIC", UcliOperationExposure.Public)]
    [InlineData("editLoweringOnly", UcliOperationExposure.EditLoweringOnly)]
    [InlineData("INTERNAL", UcliOperationExposure.Internal)]
    [InlineData("internal", UcliOperationExposure.Internal)]
    public void UcliOperationExposureCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        UcliOperationExposure expected)
    {
        var result = UcliOperationExposureCodec.TryParse(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported")]
    public void UcliOperationExposureCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(UcliOperationExposureCodec.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposureCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(UcliOperationExposureCodec.TryParse(null, out _));
    }
}
