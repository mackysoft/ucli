using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Shared;

public sealed class SkillFailureCodeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RetainsUnknownCodeValue ()
    {
        SkillFailureCode code = new("SKILL_FUTURE_FAILURE");

        Assert.Equal("SKILL_FUTURE_FAILURE", code.Value);
        Assert.Equal("SKILL_FUTURE_FAILURE", code.ToString());
        string rawValue = code;
        Assert.Equal("SKILL_FUTURE_FAILURE", rawValue);
        Assert.Equal(new SkillFailureCode("SKILL_FUTURE_FAILURE"), code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsBlankValue (string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SkillFailureCode(value!));
        Assert.False(SkillFailureCode.TryCreate(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsValidatedCode ()
    {
        var result = SkillFailureCode.TryCreate("SKILL_VALID", out var code);

        Assert.True(result);
        Assert.Equal(new SkillFailureCode("SKILL_VALID"), code);
    }
}
