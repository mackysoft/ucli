using MackySoft.Ucli.Contracts.Configuration;

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
        Assert.Equal(OperationPolicyValues.Safe, OperationPolicyCodec.ToValue(OperationPolicy.Safe));
        Assert.Equal(OperationPolicyValues.Advanced, OperationPolicyCodec.ToValue(OperationPolicy.Advanced));
        Assert.Equal(OperationPolicyValues.Dangerous, OperationPolicyCodec.ToValue(OperationPolicy.Dangerous));
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
        var result = OperationPolicyCodec.TryParse(value, out var parsed);

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
        Assert.False(OperationPolicyCodec.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicyCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(OperationPolicyCodec.TryParse(null, out _));
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
        Assert.Equal(ReadIndexModeValues.Disabled, ReadIndexModeCodec.ToValue(ReadIndexMode.Disabled));
        Assert.Equal(ReadIndexModeValues.AllowStale, ReadIndexModeCodec.ToValue(ReadIndexMode.AllowStale));
        Assert.Equal(ReadIndexModeValues.RequireFresh, ReadIndexModeCodec.ToValue(ReadIndexMode.RequireFresh));
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
        var result = ReadIndexModeCodec.TryParse(value, out var parsed);

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
        Assert.False(ReadIndexModeCodec.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexModeCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ReadIndexModeCodec.TryParse(null, out _));
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
        Assert.Equal(UcliOperationKindValues.Mutation, UcliOperationKindCodec.ToValue(UcliOperationKind.Mutation));
        Assert.Equal(UcliOperationKindValues.Query, UcliOperationKindCodec.ToValue(UcliOperationKind.Query));
        Assert.Equal(UcliOperationKindValues.Command, UcliOperationKindCodec.ToValue(UcliOperationKind.Command));
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
        var result = UcliOperationKindCodec.TryParse(value, out var parsed);

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
        Assert.False(UcliOperationKindCodec.TryParse(value, out _));
    }
}
