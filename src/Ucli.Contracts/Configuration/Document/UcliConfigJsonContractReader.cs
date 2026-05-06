using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Provides shared JSON contract readers for <c>.ucli/config.json</c>. </summary>
internal static class UcliConfigJsonContractReader
{
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
            error = new UcliConfigJsonReadError(UcliConfigJsonReadErrorKind.RootTypeMismatch);
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
