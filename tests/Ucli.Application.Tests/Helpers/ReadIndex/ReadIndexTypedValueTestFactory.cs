using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class ReadIndexTypedValueTestFactory
{
    public static AssetLookupSnapshot CreateAssetLookupSnapshot (IpcIndexAssetsReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!IndexCatalogContractValidator.TryProjectAssetSearchEntries(
                response.AssetSearchEntries,
                "assetSearchEntries",
                out var assetSearchEntries,
                out var assetSearchError))
        {
            throw new InvalidOperationException($"Asset lookup fixture is invalid. {assetSearchError}");
        }

        if (!IndexCatalogContractValidator.TryProjectGuidPathEntries(
                response.GuidPathEntries,
                "guidPathEntries",
                out var guidPathEntries,
                out var guidPathError))
        {
            throw new InvalidOperationException($"Asset lookup fixture is invalid. {guidPathError}");
        }

        if (!AssetLookupSnapshot.TryCreate(
                response.GeneratedAtUtc,
                assetSearchEntries,
                guidPathEntries,
                out var snapshot,
                out var snapshotError))
        {
            throw new InvalidOperationException($"Asset lookup fixture is invalid. {snapshotError}");
        }

        return snapshot;
    }

    public static IReadOnlyList<SceneTreeLiteNode> CreateSceneTreeNodes (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> nodes)
    {
        if (!IndexCatalogContractValidator.TryProjectSceneTreeLiteNodes(
                nodes,
                "roots",
                out var projectedNodes,
                out var error))
        {
            throw new InvalidOperationException($"Scene-tree-lite fixture is invalid. {error}");
        }

        return projectedNodes;
    }

    public static SceneTreeLiteSourceSnapshot CreateSceneTreeSourceSnapshot (
        IpcIndexSceneTreeLiteReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new SceneTreeLiteSourceSnapshot(
            response.GeneratedAtUtc,
            response.ScenePath,
            CreateSceneTreeNodes(response.Roots),
            response.SourceState);
    }
}
