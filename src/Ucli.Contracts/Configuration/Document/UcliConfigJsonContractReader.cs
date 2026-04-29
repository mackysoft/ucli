using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;
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

        var unknownProperty = JsonObjectPropertyReader.FindUnknownProperty(root, StrictAllowedProperties);
        if (!string.IsNullOrWhiteSpace(unknownProperty))
        {
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.UnknownProperty, unknownProperty);
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredInt32(
            root,
            UcliConfigJsonPropertyNames.SchemaVersion,
            CreateMissingPropertyError,
            CreatePropertyTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var schemaVersion,
            out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            root,
            UcliConfigJsonPropertyNames.OperationPolicy,
            CreateMissingPropertyError,
            CreatePropertyTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var operationPolicy,
            out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            root,
            UcliConfigJsonPropertyNames.PlanTokenMode,
            CreateMissingPropertyError,
            CreatePropertyTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var planTokenMode,
            out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadOptionalString(
            root,
            UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
            CreatePropertyTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var readIndexDefaultMode,
            out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredStringArray(
            root,
            UcliConfigJsonPropertyNames.OperationAllowlist,
            CreateMissingPropertyError,
            CreatePropertyTypeMismatchError,
            CreateArrayElementTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var operationAllowlist,
            out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadOptionalNullableInt32(
            root,
            UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
            CreatePropertyTypeMismatchError,
            UcliConfigJsonReadError.None,
            out var ipcDefaultTimeoutMilliseconds,
            out error))
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

    private static UcliConfigJsonReadError CreateMissingPropertyError (string propertyName)
    {
        return new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.MissingProperty, propertyName);
    }

    private static UcliConfigJsonReadError CreatePropertyTypeMismatchError (string propertyName)
    {
        return new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, propertyName);
    }

    private static UcliConfigJsonReadError CreateArrayElementTypeMismatchError (string propertyName)
    {
        return new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.ArrayElementTypeMismatch, propertyName);
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

            if (!StringValueNormalizer.TryTrimToNonEmpty(element.GetString(), out var value))
            {
                continue;
            }

            values.Add(value);
        }

        return values.ToArray();
    }

}
