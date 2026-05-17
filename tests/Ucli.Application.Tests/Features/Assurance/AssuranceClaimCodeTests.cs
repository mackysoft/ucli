using MackySoft.Ucli.Application.Features.Assurance;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance;

public sealed class AssuranceClaimCodeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithValidValue_PreservesValue ()
    {
        var code = new AssuranceClaimCode("UNITY_READY_EXECUTION");

        Assert.Equal("UNITY_READY_EXECUTION", code.Value);
        Assert.Equal("UNITY_READY_EXECUTION", code.ToString());
        Assert.True(code.EqualsValue("UNITY_READY_EXECUTION"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void TryCreate_WithInvalidValue_ReturnsFalse (string? value)
    {
        var result = AssuranceClaimCode.TryCreate(value, out var code);

        Assert.False(result);
        Assert.False(code.IsValid);
    }
}
