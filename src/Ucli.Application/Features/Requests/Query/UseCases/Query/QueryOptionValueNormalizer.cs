using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Normalizes typed-query string option values. </summary>
internal static class QueryOptionValueNormalizer
{
    /// <summary> Normalizes one optional string option. </summary>
    public static bool TryNormalizeOptional (
        string? value,
        string optionName,
        out string? normalizedValue,
        out ExecutionError? error)
    {
        normalizedValue = null;
        error = null;
        if (value is null)
        {
            return true;
        }

        return TryNormalizeRequired(value, optionName, out normalizedValue, out error);
    }

    /// <summary> Normalizes one required string option. </summary>
    public static bool TryNormalizeRequired (
        string? value,
        string optionName,
        out string normalizedValue,
        out ExecutionError? error)
    {
        normalizedValue = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = ExecutionError.InvalidArgument($"Option '--{optionName}' must not be empty or whitespace.");
            return false;
        }

        var trimmedValue = value.Trim();
        if (!string.Equals(value, trimmedValue, StringComparison.Ordinal))
        {
            error = ExecutionError.InvalidArgument($"Option '--{optionName}' must not contain leading or trailing whitespace.");
            return false;
        }

        normalizedValue = value;
        return true;
    }
}
