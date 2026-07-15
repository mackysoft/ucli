using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal static class AssetLookupSnapshotTestFactory
{
    private static readonly Guid AssetGuid = Guid.ParseExact("11111111111111111111111111111111", "N");

    public static AssetLookupSnapshot Create (
        string generatedAtUtc,
        string assetPath)
    {
        var typedPath = new UnityAssetPath(assetPath);
        if (!AssetLookupSnapshot.TryCreate(
                DateTimeOffset.Parse(generatedAtUtc),
                [
                    new AssetSearchLookupEntry(
                        typedPath,
                        AssetGuid,
                        Path.GetFileNameWithoutExtension(assetPath),
                        new UnityTypeId("Game.Asset, Assembly-CSharp"),
                        [
                            new UnityTypeId("Game.Asset, Assembly-CSharp"),
                            new UnityTypeId("UnityEngine.Object, UnityEngine.CoreModule"),
                        ]),
                ],
                [
                    new GuidPathLookupEntry(AssetGuid, typedPath),
                ],
                out var snapshot,
                out var error))
        {
            throw new InvalidOperationException($"Asset lookup fixture is invalid. {error}");
        }

        return snapshot;
    }
}
