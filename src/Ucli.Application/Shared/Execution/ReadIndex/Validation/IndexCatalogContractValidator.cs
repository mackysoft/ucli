using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Validates read-index catalog contracts loaded from persistent storage. </summary>
internal static class IndexCatalogContractValidator
{
    /// <summary> Projects one operation-entry collection shared by persisted and live ops catalog payloads. </summary>
    /// <param name="entries"> The operation-entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="allowEditLoweringOnlyEntries"> Whether edit-lowering-only entries are valid for request validation. </param>
    /// <param name="requireCanonicalLiterals"> Whether enum literals must use their canonical persisted representation. </param>
    /// <param name="operations"> The validated typed operations on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectOpsEntries (
        IReadOnlyList<IndexOpEntryJsonContract>? entries,
        string propertyName,
        bool allowEditLoweringOnlyEntries,
        bool requireCanonicalLiterals,
        [NotNullWhen(true)]
        out IReadOnlyList<ValidatedOpsOperation>? operations,
        out string? error)
    {
        operations = null;
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        var projectedOperations = new ValidatedOpsOperation[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!TryProjectOpsEntry(
                    entry,
                    i,
                    allowEditLoweringOnlyEntries,
                    requireCanonicalLiterals,
                    out var operation,
                    out error))
            {
                return false;
            }

            if (!operationNames.Add(operation.Name))
            {
                error = $"Operation entry '{entry.Name}' is duplicated.";
                return false;
            }

            projectedOperations[i] = operation;
        }

        operations = Array.AsReadOnly(projectedOperations);
        error = null;
        return true;
    }

    /// <summary> Projects one operation entry shared by persisted detail and live catalog payloads. </summary>
    /// <param name="entry"> The operation entry. </param>
    /// <param name="index"> The entry index used in validation errors. </param>
    /// <param name="allowEditLoweringOnlyEntries"> Whether edit-lowering-only entries are valid for request validation. </param>
    /// <param name="requireCanonicalLiterals"> Whether enum literals must use their canonical persisted representation. </param>
    /// <param name="operation"> The validated typed operation on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the operation entry is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectOpsEntry (
        IndexOpEntryJsonContract? entry,
        int index,
        bool allowEditLoweringOnlyEntries,
        bool requireCanonicalLiterals,
        [NotNullWhen(true)]
        out ValidatedOpsOperation? operation,
        out string? error)
    {
        operation = null;
        error = null;
        if (entry == null
            || string.IsNullOrWhiteSpace(entry.Name)
            || !TryParseContractLiteral(entry.Kind, requireCanonicalLiterals, out UcliOperationKind kind)
            || !TryParseContractLiteral(entry.Policy, requireCanonicalLiterals, out OperationPolicy policy)
            || !TryParseContractLiteral(
                entry.PlayModeSupport,
                requireCanonicalLiterals,
                out UcliOperationPlayModeSupport playModeSupport)
            || !TryResolveCatalogExposure(
                entry.Exposure,
                allowEditLoweringOnlyEntries,
                requireCanonicalLiterals,
                out var exposure,
                out error)
            || !TryParseArgsSchema(entry.ArgsSchemaJson, exposure, out var argsSchema)
            || !TryParseOptionalSchema(entry.ResultSchemaJson, out var resultSchema)
            || !TryValidateOpsDescribeContract(entry, exposure, out error))
        {
            error ??= $"Operation entry at index {index} is invalid.";
            return false;
        }

        operation = new ValidatedOpsOperation(
            entry,
            kind,
            policy,
            exposure,
            playModeSupport,
            argsSchema,
            resultSchema);
        error = null;
        return true;
    }

    private static bool TryParseContractLiteral<T> (
        string? value,
        bool requireCanonical,
        out T parsedValue)
        where T : struct, Enum
    {
        return requireCanonical
            ? ContractLiteralCodec.TryParse(value, out parsedValue)
            : ContractLiteralInputParser.TryParseIgnoreCase(value, out parsedValue);
    }

    private static bool TryResolveCatalogExposure (
        string? exposureValue,
        bool allowEditLoweringOnlyEntries,
        bool requireCanonicalLiteral,
        out UcliOperationExposure exposure,
        out string? error)
    {
        if (exposureValue == null)
        {
            exposure = UcliOperationExposure.Public;
            error = null;
            return true;
        }

        if (!TryParseContractLiteral(exposureValue, requireCanonicalLiteral, out exposure))
        {
            error = $"Unsupported operation exposure '{exposureValue}'.";
            return false;
        }

        if (exposure == UcliOperationExposure.Public)
        {
            error = null;
            return true;
        }

        if (exposure == UcliOperationExposure.EditLoweringOnly && allowEditLoweringOnlyEntries)
        {
            error = null;
            return true;
        }

        error = $"Operation exposure '{exposureValue}' is not allowed in this catalog.";
        return false;
    }

    private static bool TryParseArgsSchema (
        string? argsSchemaJson,
        UcliOperationExposure exposure,
        out JsonElement schema)
    {
        return exposure == UcliOperationExposure.EditLoweringOnly
            ? IndexJsonSchemaSubsetValidator.TryParseObjectSchema(argsSchemaJson, out schema)
            : IndexJsonSchemaSubsetValidator.TryParsePublicRawOpArgsSchema(argsSchemaJson, out schema);
    }

    private static bool TryParseOptionalSchema (
        string? json,
        out JsonElement? schema)
    {
        if (json == null)
        {
            schema = null;
            return true;
        }

        if (!IndexJsonSchemaSubsetValidator.TryParseObjectSchema(json, out var parsedSchema))
        {
            schema = null;
            return false;
        }

        schema = parsedSchema;
        return true;
    }

    private static bool TryValidateOpsDescribeContract (
        IndexOpEntryJsonContract entry,
        UcliOperationExposure exposure,
        out string? error)
    {
        var describeContract = new UcliOperationDescribeContract(
            entry.Description,
            entry.Inputs,
            entry.ResultContract,
            entry.Assurance,
            entry.CodeContract);
        var ownerName = $"Operation entry '{entry.Name}'";
        string inputError;
        bool describeContractValid;
        if (exposure == UcliOperationExposure.Public)
        {
            describeContractValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describeContract,
                entry.Kind,
                entry.Policy,
                ownerName,
                out inputError);
        }
        else
        {
            describeContractValid = UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContract(
                describeContract,
                entry.Kind,
                entry.Policy,
                ownerName,
                exposure,
                out inputError);
        }

        if (!describeContractValid)
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

    /// <summary> Projects one asset-search entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="projectedEntries"> The typed entries on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectAssetSearchEntries (
        IReadOnlyList<IndexAssetSearchEntryJsonContract>? entries,
        string propertyName,
        [NotNullWhen(true)]
        out IReadOnlyList<AssetSearchLookupEntry>? projectedEntries,
        out string? error)
    {
        projectedEntries = null;
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<UnityAssetPath>();
        var assetGuids = new HashSet<Guid>();
        var projected = new AssetSearchLookupEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            if (!TryProjectAssetSearchEntry(entries[i], out var entry))
            {
                error = $"Asset-search entry at index {i} is invalid.";
                return false;
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Asset-search entry '{entry.AssetPath.Value}' is duplicated.";
                return false;
            }

            if (entry.AssetGuid is { } assetGuid
                && !assetGuids.Add(assetGuid))
            {
                error = $"Asset-search assetGuid '{assetGuid:N}' is duplicated.";
                return false;
            }

            projected[i] = entry;
        }

        projectedEntries = Array.AsReadOnly(projected);
        error = null;
        return true;
    }

    /// <summary> Projects one GUID-path entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="projectedEntries"> The typed entries on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectGuidPathEntries (
        IReadOnlyList<IndexGuidPathEntryJsonContract>? entries,
        string propertyName,
        [NotNullWhen(true)]
        out IReadOnlyList<GuidPathLookupEntry>? projectedEntries,
        out string? error)
    {
        projectedEntries = null;
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<UnityAssetPath>();
        var assetGuids = new HashSet<Guid>();
        var projected = new GuidPathLookupEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            if (!TryProjectGuidPathEntry(entries[i], out var entry))
            {
                error = $"Guid-path entry at index {i} is invalid.";
                return false;
            }

            if (!assetGuids.Add(entry.AssetGuid))
            {
                error = $"Guid-path assetGuid '{entry.AssetGuid:N}' is duplicated.";
                return false;
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Guid-path entry '{entry.AssetPath.Value}' is duplicated.";
                return false;
            }

            projected[i] = entry;
        }

        projectedEntries = Array.AsReadOnly(projected);
        error = null;
        return true;
    }

    /// <summary> Projects one scene-tree-lite node collection shared by persisted and live payloads. </summary>
    /// <param name="nodes"> The node collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="projectedNodes"> The typed nodes on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the node collection is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectSceneTreeLiteNodes (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? nodes,
        string propertyName,
        [NotNullWhen(true)]
        out IReadOnlyList<SceneTreeLiteNode>? projectedNodes,
        out string? error)
    {
        projectedNodes = null;
        if (nodes == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var projected = new SceneTreeLiteNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!TryProjectSceneTreeLiteNode(
                    nodes[i],
                    $"{propertyName}[{i}]",
                    out var node,
                    out error))
            {
                return false;
            }

            projected[i] = node;
        }

        projectedNodes = Array.AsReadOnly(projected);
        error = null;
        return true;
    }

    private static bool TryProjectAssetSearchEntry (
        IndexAssetSearchEntryJsonContract? contract,
        [NotNullWhen(true)]
        out AssetSearchLookupEntry? entry)
    {
        entry = null;
        if (contract == null)
        {
            return false;
        }

        var assetPathText = contract.AssetPath;
        var assetGuidText = contract.AssetGuid;
        var name = contract.Name;
        var typeIdText = contract.TypeId;
        var searchTypeIds = contract.SearchTypeIds;
        if (!UnityAssetPath.TryParseCanonical(assetPathText, out var assetPath)
            || assetGuidText == null
            || name == null
            || string.IsNullOrWhiteSpace(name)
            || !UnityTypeId.TryParse(typeIdText, out var typeId)
            || searchTypeIds == null
            || searchTypeIds.Count == 0)
        {
            return false;
        }

        Guid? assetGuid = null;
        if (assetGuidText.Length > 0)
        {
            if (!TryParseCanonicalAssetGuid(assetGuidText, out var parsedAssetGuid))
            {
                return false;
            }

            assetGuid = parsedAssetGuid;
        }

        var projectedSearchTypeIds = new UnityTypeId[searchTypeIds.Count];
        for (var i = 0; i < searchTypeIds.Count; i++)
        {
            if (!UnityTypeId.TryParse(searchTypeIds[i], out var searchTypeId))
            {
                return false;
            }

            projectedSearchTypeIds[i] = searchTypeId;
        }

        entry = new AssetSearchLookupEntry(
            assetPath,
            assetGuid,
            name,
            typeId,
            projectedSearchTypeIds);
        return true;
    }

    private static bool TryProjectGuidPathEntry (
        IndexGuidPathEntryJsonContract? contract,
        [NotNullWhen(true)]
        out GuidPathLookupEntry? entry)
    {
        entry = null;
        if (contract == null
            || !TryParseCanonicalAssetGuid(contract.AssetGuid, out var assetGuid))
        {
            return false;
        }

        if (!UnityAssetPath.TryParseCanonical(contract.AssetPath, out var assetPath))
        {
            return false;
        }

        entry = new GuidPathLookupEntry(
            assetGuid,
            assetPath);
        return true;
    }

    private static bool TryParseCanonicalAssetGuid (
        string? value,
        out Guid assetGuid)
    {
        assetGuid = Guid.Empty;
        if (value == null || value.Length != 32)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return Guid.TryParseExact(value, "N", out assetGuid)
            && assetGuid != Guid.Empty;
    }

    private static bool TryProjectSceneTreeLiteNode (
        IndexSceneTreeLiteNodeJsonContract? contract,
        string propertyName,
        [NotNullWhen(true)]
        out SceneTreeLiteNode? node,
        out string? error)
    {
        node = null;
        if (contract == null
            || contract.Name == null
            || contract.GlobalObjectId == null
            || contract.Children == null
            || !IsSourceSceneTreeLiteChildrenState(contract.ChildrenState))
        {
            error = $"Scene-tree-lite node '{propertyName}' is invalid.";
            return false;
        }

        UnityGlobalObjectId? globalObjectId = null;
        if (contract.GlobalObjectId.Length > 0
            && !UnityGlobalObjectId.TryParse(contract.GlobalObjectId, out globalObjectId))
        {
            error = $"Scene-tree-lite node '{propertyName}' is invalid.";
            return false;
        }

        if (!TryProjectSceneTreeLiteNodes(
                contract.Children,
                $"{propertyName}.children",
                out var children,
                out error))
        {
            return false;
        }

        node = new SceneTreeLiteNode(
            contract.Name,
            globalObjectId,
            children,
            contract.ChildrenState);
        error = null;
        return true;
    }

    private static bool IsSourceSceneTreeLiteChildrenState (IndexSceneTreeLiteNodeChildrenState childrenState)
    {
        return childrenState is IndexSceneTreeLiteNodeChildrenState.Complete
            or IndexSceneTreeLiteNodeChildrenState.NotExpandedByDepth
            or IndexSceneTreeLiteNodeChildrenState.Unknown;
    }
}
