using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides reusable contract readers for JSON string properties. </summary>
internal static class JsonStringContractReader
{
    /// <summary> Reads one JSON string property based on configured presence and content requirements. </summary>
    /// <param name="jsonObject"> The source JSON object. </param>
    /// <param name="propertyName"> The target property name. </param>
    /// <param name="presenceRequirement"> The property presence requirement. </param>
    /// <param name="rejectEmptyOrWhitespace"> Whether empty or whitespace-only values are rejected. </param>
    /// <param name="rejectOuterWhitespace"> Whether leading or trailing whitespace is rejected. </param>
    /// <param name="value"> The parsed string value, or <see langword="null" /> when absent. </param>
    /// <param name="error"> The machine-readable read error when parsing fails. </param>
    /// <returns> <see langword="true" /> when read succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryRead (
        JsonElement jsonObject,
        string propertyName,
        JsonStringPresenceRequirement presenceRequirement,
        bool rejectEmptyOrWhitespace,
        bool rejectOuterWhitespace,
        out string? value,
        out JsonStringReadError error)
    {
        value = null;
        error = JsonStringReadError.None;

        if (!jsonObject.TryGetProperty(propertyName, out var propertyElement))
        {
            if (presenceRequirement == JsonStringPresenceRequirement.Required)
            {
                error = new JsonStringReadError(JsonStringReadErrorKind.Missing, propertyName);
                return false;
            }

            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            if (presenceRequirement == JsonStringPresenceRequirement.OptionalLoose)
            {
                return true;
            }

            error = new JsonStringReadError(JsonStringReadErrorKind.TypeMismatch, propertyName);
            return false;
        }

        var parsedValue = propertyElement.GetString() ?? string.Empty;
        if (rejectOuterWhitespace && StringValueValidator.HasOuterWhitespace(parsedValue))
        {
            error = new JsonStringReadError(JsonStringReadErrorKind.OuterWhitespace, propertyName);
            return false;
        }

        if (rejectEmptyOrWhitespace && string.IsNullOrWhiteSpace(parsedValue))
        {
            error = new JsonStringReadError(JsonStringReadErrorKind.EmptyOrWhitespace, propertyName);
            return false;
        }

        value = parsedValue;
        return true;
    }
}