using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Parses public CLI code references before projecting them into catalog payloads. </summary>
internal static class CodeCliArgumentParser
{
    /// <summary> Tries to parse a CLI-safe code reference from <c>CODE</c> or <c>KIND:CODE</c>. </summary>
    /// <param name="value"> The raw command-line argument. </param>
    /// <param name="reference"> The parsed code reference when successful. </param>
    /// <param name="errorMessage"> The invalid-argument message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out CodeCatalogCodeReference reference,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reference = null!;
            errorMessage = "Code must not be empty.";
            return false;
        }

        CodeCatalogKind? expectedKind = null;
        var code = value;
        var kindSeparatorIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (kindSeparatorIndex >= 0)
        {
            if (value.IndexOf(':', kindSeparatorIndex + 1) >= 0)
            {
                reference = null!;
                errorMessage = "Code reference must contain at most one kind separator.";
                return false;
            }

            var kind = value[..kindSeparatorIndex];
            code = value[(kindSeparatorIndex + 1)..];
            if (string.IsNullOrWhiteSpace(kind))
            {
                reference = null!;
                errorMessage = "Code kind must not be empty.";
                return false;
            }

            if (!ContractLiteralCodec.TryParse<CodeCatalogKind>(kind, out var parsedKind))
            {
                reference = null!;
                errorMessage = "Code kind is unsupported.";
                return false;
            }

            expectedKind = parsedKind;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            reference = null!;
            errorMessage = "Code must not be empty.";
            return false;
        }

        if (!UcliCode.TryCreate(code, out var codeValue))
        {
            reference = null!;
            errorMessage = UcliCode.InvalidValueMessage;
            return false;
        }

        reference = new CodeCatalogCodeReference(codeValue, expectedKind);
        errorMessage = string.Empty;
        return true;
    }
}
