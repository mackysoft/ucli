using MackySoft.Ucli.Hosting.Cli.Errors;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Errors;

public sealed class ErrorCodeCliArgumentValidatorTests
{
    [Theory]
    [InlineData("SOME_FUTURE_CODE")]
    [InlineData("SOME.FUTURE_CODE")]
    [InlineData("A1_B2")]
    [Trait("Size", "Small")]
    public void TryCreate_WithValidToken_ReturnsCode (string value)
    {
        var result = ErrorCodeCliArgumentValidator.TryCreate(value, out var code, out var errorMessage);

        Assert.True(result);
        Assert.Equal(value, code.Value);
        Assert.Empty(errorMessage);
    }

    [Theory]
    [MemberData(nameof(InvalidCodeTokens))]
    [Trait("Size", "Small")]
    public void TryCreate_WithInvalidToken_ReturnsFailure (string? value)
    {
        var result = ErrorCodeCliArgumentValidator.TryCreate(value, out var code, out var errorMessage);

        Assert.False(result);
        Assert.False(code.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    public static TheoryData<string?> InvalidCodeTokens { get; } =
    [
        null,
        string.Empty,
        " ",
        "not a code",
        "lowercase_code",
        "CODE.",
        ".CODE",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', 129),
    ];
}
