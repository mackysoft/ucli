using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class ContractLiteralInputParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedIgnoreCase_WithCaseVariant_ReturnsTrue ()
    {
        Assert.True(ContractLiteralInputParser.IsDefinedIgnoreCase<OperationPolicy>("SAFE"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedIgnoreCase_WithWhitespace_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<OperationPolicy>(" safe "));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedTrimmed_WithWhitespace_ReturnsTrue ()
    {
        Assert.True(ContractLiteralInputParser.IsDefinedTrimmed<OperationPolicy>(" safe "));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedTrimmed_WithCaseVariant_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedTrimmed<OperationPolicy>("SAFE"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseIgnoreCase_WithCaseVariant_ReturnsEnumValue ()
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<OperationPolicy>("SAFE", out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseIgnoreCase_WithWhitespace_ReturnsFalse ()
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<OperationPolicy>(" safe ", out var policy);

        Assert.False(result);
        Assert.Equal(default, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseTrimmed_WithWhitespace_ReturnsEnumValue ()
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<OperationPolicy>(" safe ", out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseTrimmed_WithCaseVariant_ReturnsFalse ()
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<OperationPolicy>("SAFE", out var policy);

        Assert.False(result);
        Assert.Equal(default, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseTrimmedIgnoreCase_WithWhitespaceAndCaseVariant_ReturnsEnumValue ()
    {
        var result = ContractLiteralInputParser.TryParseTrimmedIgnoreCase<OperationPolicy>(" SAFE ", out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsDefinedTrimmedIgnoreCase_WithMissingLiteral_ReturnsFalse (string? literal)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedTrimmedIgnoreCase<OperationPolicy>(literal));
    }
}
