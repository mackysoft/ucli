using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Provides reusable property readers for public <c>kind:"edit"</c> step parsing. </summary>
internal static class IpcEditStepContractReadHelpers
{
    public static bool TryReadRequiredObject (
        JsonElement jsonObject,
        string propertyName,
        string propertyPath,
        out JsonElement propertyElement,
        out string errorMessage)
    {
        propertyElement = default;
        errorMessage = string.Empty;
        if (!jsonObject.TryGetProperty(propertyName, out propertyElement))
        {
            errorMessage = $"Edit step property '{propertyPath}' is required.";
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = $"Edit step property '{propertyPath}' must be an object.";
            return false;
        }

        return true;
    }

    public static bool TryReadRequiredArray (
        JsonElement jsonObject,
        string propertyName,
        string propertyPath,
        out JsonElement propertyElement,
        out string errorMessage)
    {
        propertyElement = default;
        errorMessage = string.Empty;
        if (!jsonObject.TryGetProperty(propertyName, out propertyElement))
        {
            errorMessage = $"Edit step property '{propertyPath}' is required.";
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Array)
        {
            errorMessage = $"Edit step property '{propertyPath}' must be an array.";
            return false;
        }

        return true;
    }

    public static bool TryReadRequiredString (
        JsonElement jsonObject,
        string propertyName,
        string propertyPath,
        out string? value,
        out string errorMessage)
    {
        value = null;
        errorMessage = string.Empty;
        if (!jsonObject.TryGetProperty(propertyName, out var propertyElement))
        {
            errorMessage = $"Edit step property '{propertyPath}' is required.";
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"Edit step property '{propertyPath}' must be a string.";
            return false;
        }

        value = propertyElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not be empty.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not contain leading or trailing whitespace.";
            return false;
        }

        return true;
    }

    public static string? TryReadOptionalString (
        JsonElement jsonObject,
        string propertyName,
        string propertyPath,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!jsonObject.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"Edit step property '{propertyPath}' must be a string.";
            return null;
        }

        var value = propertyElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not be empty.";
            return null;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not contain leading or trailing whitespace.";
            return null;
        }

        return value;
    }

    public static bool TryReadUniqueString (
        JsonProperty property,
        string propertyPath,
        ref bool hasProperty,
        out string? value,
        out string errorMessage)
    {
        value = null;
        errorMessage = string.Empty;
        if (hasProperty)
        {
            errorMessage = $"Edit step property '{propertyPath}' is duplicated.";
            return false;
        }

        if (property.Value.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"Edit step property '{propertyPath}' must be a string.";
            return false;
        }

        value = property.Value.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not be empty.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"Edit step property '{propertyPath}' must not contain leading or trailing whitespace.";
            return false;
        }

        hasProperty = true;
        return true;
    }

    public static int CountTrue (bool a, bool b, bool c, bool d)
    {
        var count = 0;
        if (a)
        {
            count++;
        }

        if (b)
        {
            count++;
        }

        if (c)
        {
            count++;
        }

        if (d)
        {
            count++;
        }

        return count;
    }
}
