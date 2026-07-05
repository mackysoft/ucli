using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Codes;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Codes;

public sealed class CodeCliArgumentParserTests
{
    private static readonly ValidCodeReferenceCase[] ValidCodeReferences =
    [
        new("SOME_FUTURE_CODE", null, "SOME_FUTURE_CODE"),
        new("SOME.FUTURE_CODE", null, "SOME.FUTURE_CODE"),
        new("A1_B2", null, "A1_B2"),
        new("error:IPC_TIMEOUT", CodeCatalogKindValues.Error, "IPC_TIMEOUT"),
        new("future-kind:SOME_FUTURE_CODE", "future-kind", "SOME_FUTURE_CODE"),
        new("unknown:IPC_TIMEOUT", CodeCatalogKindValues.Unknown, "IPC_TIMEOUT"),
    ];

    private static readonly string?[] InvalidCodeReferences =
    [
        null,
        string.Empty,
        " ",
        "not a code",
        "lowercase_code",
        "Code:IPC_TIMEOUT",
        "kind with space:IPC_TIMEOUT",
        "-kind:IPC_TIMEOUT",
        "error:",
        ":IPC_TIMEOUT",
        "error:IPC:TIMEOUT",
        "CODE.",
        ".CODE",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', 129),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithValidReference_ReturnsParsedReference ()
    {
        foreach (var testCase in ValidCodeReferences)
        {
            var result = CodeCliArgumentParser.TryParse(testCase.Value, out var reference, out var errorMessage);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedCode, reference.Code);
            Assert.Equal(testCase.ExpectedKind, reference.ExpectedKind);
            Assert.Empty(errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithInvalidReference_ReturnsFailure ()
    {
        foreach (var value in InvalidCodeReferences)
        {
            var result = CodeCliArgumentParser.TryParse(value, out var reference, out var errorMessage);

            Assert.False(result);
            Assert.Null(reference);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }
    }

    private sealed record ValidCodeReferenceCase (
        string Value,
        string? ExpectedKind,
        string ExpectedCode);
}
