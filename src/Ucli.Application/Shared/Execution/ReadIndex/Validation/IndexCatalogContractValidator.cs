using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Validates read-index catalog contracts loaded from persistent storage. </summary>
internal static class IndexCatalogContractValidator
{
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
