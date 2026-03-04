using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Index;

/// <summary> Validates read-index catalog contracts loaded from persistent storage. </summary>
internal static class IndexCatalogContractValidator
{
    /// <summary> Validates one <c>types.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidTypesCatalog (IndexTypesCatalogJsonContract contract)
    {
        if (contract.SchemaVersion <= 0
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

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
        }

        return true;
    }

    /// <summary> Validates one <c>schemas.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidSchemasCatalog (IndexSchemasCatalogJsonContract contract)
    {
        if (contract.SchemaVersion <= 0
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

        for (var i = 0; i < contract.Entries.Count; i++)
        {
            var entry = contract.Entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.SchemaKey)
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || string.IsNullOrWhiteSpace(entry.DisplayName)
                || !IndexSchemaKindCodec.TryParse(entry.Kind, out _)
                || entry.Properties == null)
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
        return contract.SchemaVersion > 0
            && !string.IsNullOrWhiteSpace(contract.ScriptAssembliesHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesManifestHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesLockHash)
            && !string.IsNullOrWhiteSpace(contract.AssemblyDefinitionHash)
            && !string.IsNullOrWhiteSpace(contract.CombinedHash);
    }
}
