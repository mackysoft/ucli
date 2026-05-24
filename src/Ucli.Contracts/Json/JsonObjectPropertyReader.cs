using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Provides reusable JSON object property readers for strict contract parsing. </summary>
internal static class JsonObjectPropertyReader
{
    /// <summary> Finds the first unknown property in one JSON object. </summary>
    /// <param name="jsonObject"> The source JSON object. </param>
    /// <param name="allowedProperties"> The allowed property-name set. </param>
    /// <returns> The unknown property name, or <see langword="null" /> when all properties are allowed. </returns>
    public static string? FindUnknownProperty (
        JsonElement jsonObject,
        ISet<string> allowedProperties)
    {
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }

    /// <summary> Reads one required property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="missingErrorFactory"> The error factory used when property is missing. </param>
    /// <param name="property"> The parsed property value when successful. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" /> from caller-level chain. </param>
    /// <returns> <see langword="true" /> when property exists; otherwise <see langword="false" />. </returns>
    public static bool TryReadRequiredProperty<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> missingErrorFactory,
        out JsonElement property,
        out TError error)
    {
        if (!root.TryGetProperty(propertyName, out property))
        {
            error = missingErrorFactory(propertyName);
            return false;
        }

        error = default!;
        return true;
    }

    /// <summary> Reads one required int32 property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="missingErrorFactory"> The error factory used when property is missing. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="value"> The parsed int32 value when successful. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is valid int32; otherwise <see langword="false" />. </returns>
    public static bool TryReadRequiredInt32<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> missingErrorFactory,
        Func<string, TError> typeMismatchErrorFactory,
        TError noError,
        out int value,
        out TError error)
    {
        value = default;
        if (!TryReadRequiredProperty(root, propertyName, missingErrorFactory, out var property, out error))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out value))
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        error = noError;
        return true;
    }

    /// <summary> Reads one required string property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="missingErrorFactory"> The error factory used when property is missing. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="value"> The parsed string value when successful. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is valid string; otherwise <see langword="false" />. </returns>
    public static bool TryReadRequiredString<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> missingErrorFactory,
        Func<string, TError> typeMismatchErrorFactory,
        TError noError,
        out string value,
        out TError error)
    {
        value = string.Empty;
        if (!TryReadRequiredProperty(root, propertyName, missingErrorFactory, out var property, out error))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        value = property.GetString() ?? string.Empty;
        error = noError;
        return true;
    }

    /// <summary> Reads one required nullable-string property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="missingErrorFactory"> The error factory used when property is missing. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="value"> The parsed nullable-string value when successful. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is valid string or null; otherwise <see langword="false" />. </returns>
    public static bool TryReadRequiredNullableString<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> missingErrorFactory,
        Func<string, TError> typeMismatchErrorFactory,
        TError noError,
        out string? value,
        out TError error)
    {
        value = null;
        if (!TryReadRequiredProperty(root, propertyName, missingErrorFactory, out var property, out error))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            error = noError;
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        value = property.GetString();
        error = noError;
        return true;
    }

    /// <summary> Reads one optional string property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The optional property name. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="value"> The parsed value when present; otherwise <see langword="null" />. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is absent or valid string; otherwise <see langword="false" />. </returns>
    public static bool TryReadOptionalString<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> typeMismatchErrorFactory,
        TError noError,
        out string? value,
        out TError error)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            error = noError;
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        value = property.GetString();
        error = noError;
        return true;
    }

    /// <summary> Reads one optional nullable-int32 property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The optional property name. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="value"> The parsed value when present and valid; otherwise <see langword="null" />. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is absent or valid int32/null; otherwise <see langword="false" />. </returns>
    public static bool TryReadOptionalNullableInt32<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> typeMismatchErrorFactory,
        TError noError,
        out int? value,
        out TError error)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            error = noError;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            error = noError;
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsedValue))
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        value = parsedValue;
        error = noError;
        return true;
    }

    /// <summary> Reads one required string-array property from JSON object. </summary>
    /// <typeparam name="TError"> The parse error type. </typeparam>
    /// <param name="root"> The source JSON root object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="missingErrorFactory"> The error factory used when property is missing. </param>
    /// <param name="typeMismatchErrorFactory"> The error factory used when property type is invalid. </param>
    /// <param name="arrayElementTypeMismatchErrorFactory"> The error factory used when array element type is invalid. </param>
    /// <param name="noError"> The no-error value for <typeparamref name="TError" />. </param>
    /// <param name="values"> The parsed string array when successful. </param>
    /// <param name="error"> The parse error when reading fails; otherwise <paramref name="noError" />. </param>
    /// <returns> <see langword="true" /> when property is valid string array; otherwise <see langword="false" />. </returns>
    public static bool TryReadRequiredStringArray<TError> (
        JsonElement root,
        string propertyName,
        Func<string, TError> missingErrorFactory,
        Func<string, TError> typeMismatchErrorFactory,
        Func<string, TError> arrayElementTypeMismatchErrorFactory,
        TError noError,
        out string[] values,
        out TError error)
    {
        values = Array.Empty<string>();
        if (!TryReadRequiredProperty(root, propertyName, missingErrorFactory, out var property, out error))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            error = typeMismatchErrorFactory(propertyName);
            return false;
        }

        if (!TryReadStringArray(property, propertyName, arrayElementTypeMismatchErrorFactory, out values, out error))
        {
            return false;
        }

        error = noError;
        return true;
    }

    private static bool TryReadStringArray<TError> (
        JsonElement property,
        string propertyName,
        Func<string, TError> arrayElementTypeMismatchErrorFactory,
        out string[] values,
        out TError error)
    {
        values = Array.Empty<string>();
        var parsedValues = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                error = arrayElementTypeMismatchErrorFactory(propertyName);
                return false;
            }

            parsedValues.Add(element.GetString() ?? string.Empty);
        }

        values = parsedValues.ToArray();
        error = default!;
        return true;
    }
}
