using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class ConfigModeCodecContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicy_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)OperationPolicy.Safe);
        Assert.Equal(1, (int)OperationPolicy.Advanced);
        Assert.Equal(2, (int)OperationPolicy.Dangerous);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicyCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("safe", ContractLiteralCodec.ToValue(OperationPolicy.Safe));
        Assert.Equal("advanced", ContractLiteralCodec.ToValue(OperationPolicy.Advanced));
        Assert.Equal("dangerous", ContractLiteralCodec.ToValue(OperationPolicy.Dangerous));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("safe", OperationPolicy.Safe)]
    [InlineData("SAFE", OperationPolicy.Safe)]
    [InlineData("advanced", OperationPolicy.Advanced)]
    [InlineData("dangerous", OperationPolicy.Dangerous)]
    public void OperationPolicyCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        OperationPolicy expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<OperationPolicy>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported")]
    public void OperationPolicyCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<OperationPolicy>(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicyCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<OperationPolicy>(null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)ReadIndexMode.Disabled);
        Assert.Equal(1, (int)ReadIndexMode.AllowStale);
        Assert.Equal(2, (int)ReadIndexMode.RequireFresh);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexModeCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("disabled", ContractLiteralCodec.ToValue(ReadIndexMode.Disabled));
        Assert.Equal("allowStale", ContractLiteralCodec.ToValue(ReadIndexMode.AllowStale));
        Assert.Equal("requireFresh", ContractLiteralCodec.ToValue(ReadIndexMode.RequireFresh));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("disabled", ReadIndexMode.Disabled)]
    [InlineData("DISABLED", ReadIndexMode.Disabled)]
    [InlineData("allowStale", ReadIndexMode.AllowStale)]
    [InlineData("requireFresh", ReadIndexMode.RequireFresh)]
    public void ReadIndexModeCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        ReadIndexMode expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<ReadIndexMode>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported")]
    public void ReadIndexModeCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<ReadIndexMode>(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexModeCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<ReadIndexMode>(null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UcliOperationKind.Query);
        Assert.Equal(1, (int)UcliOperationKind.Mutation);
        Assert.Equal(2, (int)UcliOperationKind.Command);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationKindCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("mutation", ContractLiteralCodec.ToValue(UcliOperationKind.Mutation));
        Assert.Equal("query", ContractLiteralCodec.ToValue(UcliOperationKind.Query));
        Assert.Equal("command", ContractLiteralCodec.ToValue(UcliOperationKind.Command));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("mutation", UcliOperationKind.Mutation)]
    [InlineData("MUTATION", UcliOperationKind.Mutation)]
    [InlineData("query", UcliOperationKind.Query)]
    [InlineData("COMMAND", UcliOperationKind.Command)]
    [InlineData("command", UcliOperationKind.Command)]
    public void UcliOperationKindCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        UcliOperationKind expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<UcliOperationKind>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported")]
    public void UcliOperationKindCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<UcliOperationKind>(value));
    }
}
