using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Validates the common non-empty text invariant of request DTO properties.</summary>
internal static class UcliRequestJsonTextContractValidator
{
    public static bool TryValidateOptional (
        string? value,
        string path,
        out string errorMessage)
    {
        if (value == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidate(value, path, out errorMessage);
    }

    public static bool TryValidate (
        string? value,
        string path,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Request property '{path}' must not be empty.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"Request property '{path}' must not contain leading or trailing whitespace.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
