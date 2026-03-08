using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Index;

/// <summary> Validates read-index catalog contracts loaded from persistent storage. </summary>
internal static class IndexCatalogContractValidator
{
    private const int SupportedSchemaVersion = 1;

    /// <summary> Validates one <c>ops.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidOpsCatalog (IndexOpsCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateOpsEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one operation-entry collection shared by persisted and live ops catalog payloads. </summary>
    /// <param name="entries"> The operation-entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateOpsEntries (
        IReadOnlyList<IndexOpEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.Name)
                || !UcliOperationKindCodec.TryParse(entry.Kind, out _)
                || !OperationPolicyCodec.TryParse(entry.Policy, out _)
                || !IsValidSchemaObject(entry.ArgsSchemaJson))
            {
                error = $"Operation entry at index {i} is invalid.";
                return false;
            }

            if (!operationNames.Add(entry.Name))
            {
                error = $"Operation entry '{entry.Name}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>types.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidTypesCatalog (IndexTypesCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

        var typeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < contract.Entries.Count; i++)
        {
            var entry = contract.Entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || string.IsNullOrWhiteSpace(entry.DisplayName)
                || string.IsNullOrWhiteSpace(entry.AssemblyName)
                || entry.Flags == null)
            {
                return false;
            }

            if (!typeIds.Add(entry.TypeId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary> Validates one <c>schemas.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidSchemasCatalog (IndexSchemasCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

        var schemaKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < contract.Entries.Count; i++)
        {
            var entry = contract.Entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.SchemaKey)
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || string.IsNullOrWhiteSpace(entry.DisplayName)
                || !IndexSchemaKindCodec.TryParse(entry.Kind, out var schemaKind)
                || entry.Properties == null)
            {
                return false;
            }

            var expectedSchemaKey = $"{IndexSchemaKindCodec.ToValue(schemaKind)}:{entry.TypeId}";
            if (!string.Equals(entry.SchemaKey, expectedSchemaKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!schemaKeys.Add(entry.SchemaKey))
            {
                return false;
            }

            for (var propertyIndex = 0; propertyIndex < entry.Properties.Count; propertyIndex++)
            {
                var property = entry.Properties[propertyIndex];
                if (property == null
                    || string.IsNullOrWhiteSpace(property.Path)
                    || string.IsNullOrWhiteSpace(property.DeclaredTypeId)
                    || !IndexPropertyTypeCodec.TryParse(property.PropertyType, out _))
                {
                    return false;
                }

                if (property.IsArray)
                {
                    if (string.IsNullOrWhiteSpace(property.ElementTypeId))
                    {
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(property.ElementTypeId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary> Validates one <c>inputs/manifest.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidInputsManifest (IndexInputsManifestJsonContract contract)
    {
        return IsSupportedSchemaVersion(contract.SchemaVersion)
            && !string.IsNullOrWhiteSpace(contract.ScriptAssembliesHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesManifestHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesLockHash)
            && !string.IsNullOrWhiteSpace(contract.AssemblyDefinitionHash)
            && !string.IsNullOrWhiteSpace(contract.CombinedHash);
    }

    private static bool IsSupportedSchemaVersion (int schemaVersion)
    {
        return schemaVersion == SupportedSchemaVersion;
    }

    private static bool IsValidSchemaObject (string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}