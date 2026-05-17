namespace MackySoft.Ucli.Contracts.Tests.Diagnostics;

public sealed class UcliCodeValueTests
{
    [Theory]
    [InlineData("IPC_TIMEOUT")]
    [InlineData("UNITY_COMPILE_NO_ERRORS")]
    [InlineData("A1_B2")]
    [InlineData("SOME.FUTURE_CODE")]
    [Trait("Size", "Small")]
    public void IsValidValue_WithValidCode_ReturnsTrue (string value)
    {
        Assert.True(UcliCodeValue.IsValidValue(value));
    }

    [Theory]
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void IsValidValue_WithInvalidCode_ReturnsFalse (string? value)
    {
        Assert.False(UcliCodeValue.IsValidValue(value));
    }

    public static TheoryData<string?> InvalidCodeValues { get; } =
    [
        null,
        string.Empty,
        " ",
        "lowercase_code",
        "Code",
        "CODE-WITH-HYPHEN",
        "1_CODE",
        ".CODE",
        "CODE.",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', UcliCodeValue.MaximumLength + 1),
    ];
}
