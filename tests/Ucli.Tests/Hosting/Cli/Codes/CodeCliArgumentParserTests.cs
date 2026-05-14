using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Codes;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Codes;

public sealed class CodeCliArgumentParserTests
{
    [Theory]
    [InlineData("SOME_FUTURE_CODE", null, "SOME_FUTURE_CODE")]
    [InlineData("SOME.FUTURE_CODE", null, "SOME.FUTURE_CODE")]
    [InlineData("A1_B2", null, "A1_B2")]
    [InlineData("error:IPC_TIMEOUT", CodeCatalogKindValues.Error, "IPC_TIMEOUT")]
    [Trait("Size", "Small")]
    public void TryParse_WithValidReference_ReturnsParsedReference (
        string value,
        string? expectedKind,
        string expectedCode)
    {
        var result = CodeCliArgumentParser.TryParse(value, out var reference, out var errorMessage);

        Assert.True(result);
        Assert.Equal(expectedCode, reference.Code);
        Assert.Equal(expectedKind, reference.ExpectedKind);
        Assert.Empty(errorMessage);
    }

    [Theory]
    [MemberData(nameof(InvalidCodeReferences))]
    [Trait("Size", "Small")]
    public void TryParse_WithInvalidReference_ReturnsFailure (string? value)
    {
        var result = CodeCliArgumentParser.TryParse(value, out var reference, out var errorMessage);

        Assert.False(result);
        Assert.Null(reference);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    public static TheoryData<string?> InvalidCodeReferences { get; } =
    [
        null,
        string.Empty,
        " ",
        "not a code",
        "lowercase_code",
        "Code:IPC_TIMEOUT",
        "unknown:IPC_TIMEOUT",
        "error:",
        ":IPC_TIMEOUT",
        "error:IPC:TIMEOUT",
        "CODE.",
        ".CODE",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', 129),
    ];
}
