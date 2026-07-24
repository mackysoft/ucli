using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class VocabularyInputParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedIgnoreCase_WithCaseVariant_ReturnsTrue ()
    {
        Assert.True(VocabularyInputParser.IsDefinedIgnoreCase<OperationPolicy>("SAFE"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefinedIgnoreCase_WithWhitespace_ReturnsFalse ()
    {
        Assert.False(VocabularyInputParser.IsDefinedIgnoreCase<OperationPolicy>(" safe "));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseIgnoreCase_WithCaseVariant_ReturnsEnumValue ()
    {
        var result = VocabularyInputParser.TryParseIgnoreCase<OperationPolicy>("SAFE", out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseIgnoreCase_WithWhitespace_ReturnsFalse ()
    {
        var result = VocabularyInputParser.TryParseIgnoreCase<OperationPolicy>(" safe ", out var policy);

        Assert.False(result);
        Assert.Equal(default, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseTrimmed_WithWhitespace_ReturnsEnumValue ()
    {
        var result = VocabularyInputParser.TryParseTrimmed<OperationPolicy>(" safe ", out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseTrimmed_WithCaseVariant_ReturnsFalse ()
    {
        var result = VocabularyInputParser.TryParseTrimmed<OperationPolicy>("SAFE", out var policy);

        Assert.False(result);
        Assert.Equal(default, policy);
    }
}
