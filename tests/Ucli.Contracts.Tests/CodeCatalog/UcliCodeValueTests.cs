namespace MackySoft.Ucli.Contracts.Tests.CodeCatalog;

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

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithValidValue_PreservesValue ()
    {
        var code = new UcliCodeValue("UNITY_READY_EXECUTION");

        Assert.Equal("UNITY_READY_EXECUTION", code.Value);
        Assert.Equal("UNITY_READY_EXECUTION", code.ToString());
        Assert.True(code.EqualsValue("UNITY_READY_EXECUTION"));
        string rawValue = code;
        Assert.Equal("UNITY_READY_EXECUTION", rawValue);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithValidValue_ReturnsCode ()
    {
        var result = UcliCodeValue.TryCreate("UNITY_READY_MUTATION", out var code);

        Assert.True(result);
        Assert.Equal("UNITY_READY_MUTATION", code.Value);
        Assert.True(code.IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void IsValidValue_WithInvalidCode_ReturnsFalse (string? value)
    {
        Assert.False(UcliCodeValue.IsValidValue(value));
    }

    [Theory]
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void TryCreate_WithInvalidValue_ReturnsFalse (string? value)
    {
        var result = UcliCodeValue.TryCreate(value, out var code);

        Assert.False(result);
        Assert.False(code.IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidCodeValues))]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidValue_Throws (string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UcliCodeValue(value!));
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
