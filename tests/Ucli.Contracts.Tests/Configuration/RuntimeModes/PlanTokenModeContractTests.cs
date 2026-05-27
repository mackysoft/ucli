using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class PlanTokenModeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)PlanTokenMode.Optional);
        Assert.Equal(1, (int)PlanTokenMode.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeContractLiteral_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("optional", ContractLiteralCodec.ToValue(PlanTokenMode.Optional));
        Assert.Equal("required", ContractLiteralCodec.ToValue(PlanTokenMode.Required));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeContractLiteral_TryParse_AcceptsKnownValuesCaseInsensitive ()
    {
        Assert.True(ContractLiteralInputParser.TryParseIgnoreCase<PlanTokenMode>("optional", out var optional));
        Assert.Equal(PlanTokenMode.Optional, optional);
        Assert.True(ContractLiteralInputParser.TryParseIgnoreCase<PlanTokenMode>("REQUIRED", out var required));
        Assert.Equal(PlanTokenMode.Required, required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeContractLiteral_TryParse_UnknownValue_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<PlanTokenMode>("unsupported"));
    }
}
