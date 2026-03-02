using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Provides shared JSON contract readers for <c>.ucli/config.json</c>. </summary>
internal static class UcliConfigJsonContractReader
{
    private static readonly HashSet<string> StrictAllowedProperties = new(StringComparer.Ordinal)
    {
        UcliConfigJsonPropertyNames.SchemaVersion,
        UcliConfigJsonPropertyNames.OperationPolicy,
        UcliConfigJsonPropertyNames.PlanTokenMode,
        UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
        UcliConfigJsonPropertyNames.OperationAllowlist,
        UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
        UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand,
    };

    /// <summary> Tries to read one strict config JSON contract used by CLI validation flow. </summary>
    /// <param name="root"> The config JSON root element. </param>
    /// <param name="document"> The parsed raw config values. </param>
    /// <param name="error"> The machine-readable contract read error when parsing fails. </param>
    /// <returns> <see langword="true" /> when strict contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryReadStrict (
        JsonElement root,
        out UcliConfigJsonRawDocument document,
        out UcliConfigJsonReadError error)
    {
        document = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.RootTypeMismatch, null);
            return false;
        }

        var unknownProperty = FindUnknownProperty(root, StrictAllowedProperties);
        if (!string.IsNullOrWhiteSpace(unknownProperty))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.UnknownProperty, unknownProperty);
            return false;
        }

        if (!TryReadRequiredInt32(root, UcliConfigJsonPropertyNames.SchemaVersion, out var schemaVersion, out error))
        {
            return false;
        }

        if (!TryReadRequiredString(root, UcliConfigJsonPropertyNames.OperationPolicy, out var operationPolicy, out error))
        {
            return false;
        }

        if (!TryReadRequiredString(root, UcliConfigJsonPropertyNames.PlanTokenMode, out var planTokenMode, out error))
        {
            return false;
        }

        if (!TryReadOptionalString(root, UcliConfigJsonPropertyNames.ReadIndexDefaultMode, out var readIndexDefaultMode, out error))
        {
            return false;
        }

        if (!TryReadRequiredStringArray(root, UcliConfigJsonPropertyNames.OperationAllowlist, out var operationAllowlist, out error))
        {
            return false;
        }

        if (!TryReadOptionalNullableInt32(root, UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds, out var ipcDefaultTimeoutMilliseconds, out error))
        {
            return false;
        }

        if (!TryReadOptionalInt32Dictionary(root, UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand, out var ipcTimeoutMillisecondsByCommand, out error))
        {
            return false;
        }

        document = new UcliConfigJsonRawDocument(
            SchemaVersion: schemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: planTokenMode,
            ReadIndexDefaultMode: readIndexDefaultMode,
            OperationAllowlist: operationAllowlist,
            IpcDefaultTimeoutMilliseconds: ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand: ipcTimeoutMillisecondsByCommand);
        error = UcliConfigJsonReadError.None;
        return true;
    }

    /// <summary> Tries to read one lenient plan-token projection from config JSON. </summary>
    /// <param name="root"> The config JSON root element. </param>
    /// <param name="document"> The parsed raw config values used by plan-token flow. </param>
    /// <param name="error"> The machine-readable contract read error when parsing fails. </param>
    /// <returns> <see langword="true" /> when root contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryReadPlanTokenLoose (
        JsonElement root,
        out UcliConfigJsonRawDocument document,
        out UcliConfigJsonReadError error)
    {
        document = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.RootTypeMismatch, null);
            return false;
        }

        var planTokenMode = TryReadLooseOptionalString(root, UcliConfigJsonPropertyNames.PlanTokenMode);
        var operationPolicy = TryReadLooseOptionalString(root, UcliConfigJsonPropertyNames.OperationPolicy);
        var operationAllowlist = TryReadLooseOptionalStringArray(root, UcliConfigJsonPropertyNames.OperationAllowlist);

        document = new UcliConfigJsonRawDocument(
            SchemaVersion: null,
            OperationPolicy: operationPolicy,
            PlanTokenMode: planTokenMode,
            ReadIndexDefaultMode: null,
            OperationAllowlist: operationAllowlist,
            IpcDefaultTimeoutMilliseconds: null,
            IpcTimeoutMillisecondsByCommand: null);
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadRequiredInt32 (
        JsonElement root,
        string propertyName,
        out int value,
        out UcliConfigJsonReadError error)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.MissingProperty, propertyName);
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number || !propertyElement.TryGetInt32(out value))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadRequiredString (
        JsonElement root,
        string propertyName,
        out string value,
        out UcliConfigJsonReadError error)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.MissingProperty, propertyName);
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        value = propertyElement.GetString() ?? string.Empty;
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadOptionalString (
        JsonElement root,
        string propertyName,
        out string? value,
        out UcliConfigJsonReadError error)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = UcliConfigJsonReadError.None;
            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        value = propertyElement.GetString();
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadRequiredStringArray (
        JsonElement root,
        string propertyName,
        out string[] values,
        out UcliConfigJsonReadError error)
    {
        values = Array.Empty<string>();
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.MissingProperty, propertyName);
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Array)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        var list = new List<string>();
        foreach (var element in propertyElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.ArrayElementTypeMismatch, propertyName);
                return false;
            }

            list.Add(element.GetString() ?? string.Empty);
        }

        values = list.ToArray();
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadOptionalNullableInt32 (
        JsonElement root,
        string propertyName,
        out int? value,
        out UcliConfigJsonReadError error)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = UcliConfigJsonReadError.None;
            return true;
        }

        if (propertyElement.ValueKind == JsonValueKind.Null)
        {
            error = UcliConfigJsonReadError.None;
            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number || !propertyElement.TryGetInt32(out var parsedValue))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        value = parsedValue;
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static bool TryReadOptionalInt32Dictionary (
        JsonElement root,
        string propertyName,
        out Dictionary<string, int?>? values,
        out UcliConfigJsonReadError error)
    {
        values = null;
        if (!root.TryGetProperty(propertyName, out var propertyElement))
        {
            error = UcliConfigJsonReadError.None;
            return true;
        }

        if (propertyElement.ValueKind == JsonValueKind.Null)
        {
            error = UcliConfigJsonReadError.None;
            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.Object)
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
            return false;
        }

        var map = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var entry in propertyElement.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.Null)
            {
                map[entry.Name] = null;
                continue;
            }

            if (entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetInt32(out var timeoutValue))
            {
                error = new UcliConfigJsonReadError(
                    UcliConfigJsonReadErrorKind.ObjectPropertyTypeMismatch,
                    $"{propertyName}.{entry.Name}");
                return false;
            }

            map[entry.Name] = timeoutValue;
        }

        values = map;
        error = UcliConfigJsonReadError.None;
        return true;
    }

    private static string? TryReadLooseOptionalString (
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return StringValueNormalizer.TrimToNull(propertyElement.GetString());
    }

    private static string[]? TryReadLooseOptionalStringArray (
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var propertyElement)
            || propertyElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var element in propertyElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = StringValueNormalizer.TrimToNull(element.GetString());
            if (value is null)
            {
                continue;
            }

            values.Add(value);
        }

        return values.ToArray();
    }

    private static string? FindUnknownProperty (
        JsonElement root,
        HashSet<string> allowedProperties)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }
}