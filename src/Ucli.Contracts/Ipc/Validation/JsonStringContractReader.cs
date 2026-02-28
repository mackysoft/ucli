using System;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Specifies presence requirements for JSON string properties. </summary>
internal enum JsonStringPresenceRequirement
{
    /// <summary> Property must be present and must be a JSON string. </summary>
    Required,

    /// <summary> Property may be absent, but must be a JSON string when specified. </summary>
    OptionalStrict,

    /// <summary> Property may be absent or non-string, and non-string values are treated as unspecified. </summary>
    OptionalLoose,
}

/// <summary> Defines machine-readable error kinds for JSON string-property reads. </summary>
internal enum JsonStringReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> Property is required but missing. </summary>
    Missing,

    /// <summary> Property exists but is not a JSON string. </summary>
    TypeMismatch,

    /// <summary> Property string is empty or whitespace only. </summary>
    EmptyOrWhitespace,

    /// <summary> Property string contains leading or trailing whitespace. </summary>
    OuterWhitespace,
}

/// <summary> Represents one JSON string-property read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="PropertyName"> The property name that failed contract validation. </param>
internal readonly record struct JsonStringReadError (
    JsonStringReadErrorKind Kind,
    string PropertyName)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static JsonStringReadError None => new(JsonStringReadErrorKind.None, string.Empty);
}

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
        if (rejectOuterWhitespace && HasOuterWhitespace(parsedValue))
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

    /// <summary> Determines whether a value contains leading or trailing whitespace. </summary>
    /// <param name="value"> The string value to inspect. </param>
    /// <returns> <see langword="true" /> when leading or trailing whitespace exists; otherwise <see langword="false" />. </returns>
    public static bool HasOuterWhitespace (string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]);
    }
}