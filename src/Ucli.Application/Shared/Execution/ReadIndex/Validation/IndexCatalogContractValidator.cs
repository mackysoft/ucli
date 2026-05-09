using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

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

        return TryValidateOpsCatalogEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one <c>ops.describe/&lt;opKey&gt;.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidOpsDescribe (IndexOpsDescribeJsonContract contract)
    {
        return IsSupportedSchemaVersion(contract.SchemaVersion)
            && !string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            && TryValidateOpsEntry(contract.Operation, 0, out _);
    }

    /// <summary> Validates one lightweight operation-descriptor collection from <c>ops.catalog.json</c>. </summary>
    /// <param name="entries"> The lightweight operation-descriptor collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the descriptor collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateOpsCatalogEntries (
        IReadOnlyList<IndexOpsCatalogEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        var describeKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.Name)
                || !UcliOperationKindCodec.TryParse(entry.Kind, out _)
                || !OperationPolicyCodec.TryParse(entry.Policy, out _)
                || !IsSha256LowerHex(entry.DescribeKey)
                || !IsSha256LowerHex(entry.DescribeHash))
            {
                error = $"Operation catalog entry at index {i} is invalid.";
                return false;
            }

            if (!operationNames.Add(entry.Name!))
            {
                error = $"Operation catalog entry '{entry.Name}' is duplicated.";
                return false;
            }

            if (!describeKeys.Add(entry.DescribeKey!))
            {
                error = $"Operation describe key '{entry.DescribeKey}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
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
            if (!TryValidateOpsEntry(entry, i, out error))
            {
                return false;
            }

            if (!operationNames.Add(entry!.Name!))
            {
                error = $"Operation entry '{entry.Name}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateOpsEntry (
        IndexOpEntryJsonContract? entry,
        int index,
        out string? error)
    {
        error = null;
        if (entry == null
            || string.IsNullOrWhiteSpace(entry.Name)
            || !UcliOperationKindCodec.TryParse(entry.Kind, out _)
            || !OperationPolicyCodec.TryParse(entry.Policy, out _)
            || !IndexJsonSchemaSubsetValidator.IsValidPublicRawOpArgsSchema(entry.ArgsSchemaJson)
            || !IsValidOptionalSchemaObject(entry.ResultSchemaJson)
            || !TryValidateOpsDescribeContract(entry, out error))
        {
            error ??= $"Operation entry at index {index} is invalid.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOpsDescribeContract (
        IndexOpEntryJsonContract entry,
        out string? error)
    {
        var describeContract = new UcliOperationDescribeContract(
            entry.Description,
            entry.Inputs,
            entry.ResultContract,
            entry.Assurance,
            entry.CodeContract);
        if (!UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describeContract, $"Operation entry '{entry.Name}'", out var inputError))
        {
            error = inputError;
            return false;
        }

        if (!TryValidateOperationResultSchema(entry, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOperationResultSchema (
        IndexOpEntryJsonContract entry,
        out string? error)
    {
        var resultContract = entry.ResultContract!;

        if (resultContract.Emitted)
        {
            if (entry.ResultSchemaJson == null
                || string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
            {
                error = $"Operation entry '{entry.Name}' has an inconsistent emitted result contract.";
                return false;
            }

            error = null;
            return true;
        }

        if (entry.ResultSchemaJson != null
            || !string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
        {
            error = $"Operation entry '{entry.Name}' has an inconsistent no-result contract.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsSha256LowerHex (string? value)
    {
        if (value == null || value.Length != 64)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }
        }

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

    /// <summary> Validates one <c>asset-search.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidAssetSearchLookup (IndexAssetSearchLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateAssetSearchEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one asset-search lookup entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateAssetSearchEntries (
        IReadOnlyList<IndexAssetSearchEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<string>(StringComparer.Ordinal);
        var assetGuids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.AssetPath)
                || entry.AssetGuid == null
                || (entry.AssetGuid.Length > 0 && string.IsNullOrWhiteSpace(entry.AssetGuid))
                || string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || entry.SearchTypeIds == null
                || entry.SearchTypeIds.Count == 0)
            {
                error = $"Asset-search entry at index {i} is invalid.";
                return false;
            }

            for (var searchTypeIndex = 0; searchTypeIndex < entry.SearchTypeIds.Count; searchTypeIndex++)
            {
                if (string.IsNullOrWhiteSpace(entry.SearchTypeIds[searchTypeIndex]))
                {
                    error = $"Asset-search entry at index {i} contains an invalid searchTypeIds value.";
                    return false;
                }
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Asset-search entry '{entry.AssetPath}' is duplicated.";
                return false;
            }

            if (entry.AssetGuid.Length > 0
                && !assetGuids.Add(entry.AssetGuid))
            {
                error = $"Asset-search assetGuid '{entry.AssetGuid}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>guid-path.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidGuidPathLookup (IndexGuidPathLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateGuidPathEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one GUID-path lookup entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateGuidPathEntries (
        IReadOnlyList<IndexGuidPathEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<string>(StringComparer.Ordinal);
        var assetGuids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.AssetGuid)
                || string.IsNullOrWhiteSpace(entry.AssetPath))
            {
                error = $"Guid-path entry at index {i} is invalid.";
                return false;
            }

            if (!assetGuids.Add(entry.AssetGuid))
            {
                error = $"Guid-path assetGuid '{entry.AssetGuid}' is duplicated.";
                return false;
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Guid-path entry '{entry.AssetPath}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>scene-tree-lite/&lt;sceneKey&gt;.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidSceneTreeLiteLookup (IndexSceneTreeLiteLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.ScenePath)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateSceneTreeLiteNodes(contract.Roots, "roots", out _);
    }

    /// <summary> Validates one scene-tree-lite node collection shared by persisted and live payloads. </summary>
    /// <param name="nodes"> The node collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the node collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateSceneTreeLiteNodes (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? nodes,
        string propertyName,
        out string? error)
    {
        if (nodes == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (!TryValidateSceneTreeLiteNode(nodes[i], $"{propertyName}[{i}]", out error))
            {
                return false;
            }
        }

        error = null;
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
            && !string.IsNullOrWhiteSpace(contract.AssetsContentHash)
            && !string.IsNullOrWhiteSpace(contract.AssetSearchHash)
            && !string.IsNullOrWhiteSpace(contract.GuidPathHash)
            && !string.IsNullOrWhiteSpace(contract.CombinedHash);
    }

    private static bool IsSupportedSchemaVersion (int schemaVersion)
    {
        return schemaVersion == SupportedSchemaVersion;
    }

    private static bool TryValidateSceneTreeLiteNode (
        IndexSceneTreeLiteNodeJsonContract? node,
        string propertyName,
        out string? error)
    {
        if (node == null
            || node.Name == null
            || node.GlobalObjectId == null
            || (node.GlobalObjectId.Length > 0 && string.IsNullOrWhiteSpace(node.GlobalObjectId))
            || node.Children == null)
        {
            error = $"Scene-tree-lite node '{propertyName}' is invalid.";
            return false;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!TryValidateSceneTreeLiteNode(node.Children[i], $"{propertyName}.children[{i}]", out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsValidSchemaObject (string? json)
    {
        return IndexJsonSchemaSubsetValidator.IsValidObjectSchema(json);
    }

    private static bool IsValidOptionalSchemaObject (string? json)
    {
        return json == null || IsValidSchemaObject(json);
    }
}
