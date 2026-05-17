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
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void TryCreate_WithInvalidValue_ReturnsFalse (string? value)
    {
        var result = AssuranceClaimCode.TryCreate(value, out var code);

        Assert.False(result);
        Assert.False(code.IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidValue_Throws (string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AssuranceClaimCode(value!));
    }

    public static TheoryData<string?> InvalidCodeValues { get; } =
    [
        null,
        string.Empty,
        " ",
        "lowercase_code",
        "CODE-WITH-HYPHEN",
        "1_CODE",
        "CODE.",
        new string('A', UcliCodeValue.MaximumLength + 1),
    ];
}
