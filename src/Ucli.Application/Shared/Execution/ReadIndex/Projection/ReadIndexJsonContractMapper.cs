using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;

/// <summary> Maps validated read-index values to raw JSON contracts at file and IPC output boundaries. </summary>
internal static class ReadIndexJsonContractMapper
{
    /// <summary> Maps validated asset-search entries to their persisted JSON representation. </summary>
    public static IReadOnlyList<IndexAssetSearchEntryJsonContract> ToJsonContracts (
        IReadOnlyList<AssetSearchLookupEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var contracts = new IndexAssetSearchEntryJsonContract[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            contracts[i] = new IndexAssetSearchEntryJsonContract(
                AssetPath: entry.AssetPath.Value,
                AssetGuid: entry.AssetGuid is { } assetGuid ? assetGuid.ToString("N") : string.Empty,
                Name: entry.Name,
                TypeId: entry.TypeId.Value,
                SearchTypeIds: ToContractValues(entry.SearchTypeIds));
        }

        return contracts;
    }

    /// <summary> Maps validated GUID-path entries to their persisted JSON representation. </summary>
    public static IReadOnlyList<IndexGuidPathEntryJsonContract> ToJsonContracts (
        IReadOnlyList<GuidPathLookupEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var contracts = new IndexGuidPathEntryJsonContract[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            contracts[i] = new IndexGuidPathEntryJsonContract(
                AssetGuid: entry.AssetGuid.ToString("N"),
                AssetPath: entry.AssetPath.Value);
        }

        return contracts;
    }

    /// <summary> Maps validated scene-tree-lite nodes to their file or IPC JSON representation. </summary>
    public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> ToJsonContracts (
        IReadOnlyList<SceneTreeLiteNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var contracts = new IndexSceneTreeLiteNodeJsonContract[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            contracts[i] = ToJsonContract(nodes[i]);
        }

        return contracts;
    }

    private static IndexSceneTreeLiteNodeJsonContract ToJsonContract (SceneTreeLiteNode node)
    {
        return new IndexSceneTreeLiteNodeJsonContract(
            name: node.Name,
            globalObjectId: node.GlobalObjectId?.Value ?? string.Empty,
            children: ToJsonContracts(node.Children),
            childrenState: node.ChildrenState);
    }

    private static IReadOnlyList<string> ToContractValues (IReadOnlyList<UnityTypeId> typeIds)
    {
        var values = new string[typeIds.Count];
        for (var i = 0; i < typeIds.Count; i++)
        {
            values[i] = typeIds[i].Value;
        }

        return values;
    }
}
