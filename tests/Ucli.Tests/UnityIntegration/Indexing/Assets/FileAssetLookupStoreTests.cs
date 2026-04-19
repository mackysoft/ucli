using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;

namespace MackySoft.Ucli.Tests.Assets;

public sealed class FileAssetLookupStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_CreatesLookupFilesAndSortsEntriesByAssetPath ()
    {
        using var scope = TestDirectories.CreateTempScope("asset-lookup-store", "write");
        var store = new FileAssetLookupStore();

        await store.Write(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            assetSearchEntries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Z.asset",
                    AssetGuid: "22222222222222222222222222222222",
                    Name: "Z",
                    TypeId: "Z.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Z.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "A",
                    TypeId: "A.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "A.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ],
            guidPathEntries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "22222222222222222222222222222222",
                    AssetPath: "Assets/Z.asset"),
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/A.asset"),
            ],
            inputSnapshot: new IndexInputHashSnapshot(
                ScriptAssembliesHash: "script",
                PackagesManifestHash: "manifest",
                PackagesLockHash: "lock",
                AssemblyDefinitionHash: "asmdef",
                AssetsContentHash: "assets",
                AssetSearchHash: "asset-search",
                GuidPathHash: "guid-path",
                CombinedHash: "combined"));

        var assetSearchPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, "fingerprint");
        var guidPathPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, "fingerprint");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");

        Assert.True(File.Exists(assetSearchPath));
        Assert.True(File.Exists(guidPathPath));
        Assert.True(File.Exists(manifestPath));

        var assetSearchLookup = IndexAssetSearchLookupJsonContractSerializer.Deserialize(await File.ReadAllTextAsync(assetSearchPath, CancellationToken.None));
        var guidPathLookup = IndexGuidPathLookupJsonContractSerializer.Deserialize(await File.ReadAllTextAsync(guidPathPath, CancellationToken.None));
        var manifest = IndexInputsManifestJsonContractSerializer.Deserialize(await File.ReadAllTextAsync(manifestPath, CancellationToken.None));

        Assert.NotNull(assetSearchLookup);
        Assert.NotNull(guidPathLookup);
        Assert.NotNull(manifest);
        Assert.Equal("Assets/A.asset", assetSearchLookup!.Entries![0].AssetPath);
        Assert.Equal("Assets/A.asset", guidPathLookup!.Entries![0].AssetPath);
        Assert.Equal("asset-search", assetSearchLookup.SourceInputsHash);
        Assert.Equal("guid-path", guidPathLookup.SourceInputsHash);
        Assert.Equal("assets", manifest!.AssetsContentHash);
        Assert.Equal("asset-search", manifest.AssetSearchHash);
        Assert.Equal("guid-path", manifest.GuidPathHash);
    }
}