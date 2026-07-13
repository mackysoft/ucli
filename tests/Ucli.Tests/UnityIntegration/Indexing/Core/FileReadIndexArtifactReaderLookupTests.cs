using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderLookupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAssetSearchLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "asset-search-success");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Spawner, Assembly-CSharp",
                        "UnityEngine.ScriptableObject, UnityEngine.CoreModule",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, fingerprint),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadAssetSearchLookupAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadGuidPathLookup_ReturnsReadIndexFormatInvalid_WhenLookupJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "guid-path-malformed");
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var lookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        FileReadIndexArtifactReaderTestSupport.WriteText(lookupPath, "{");

        var result = await reader.ReadGuidPathLookupAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSceneTreeLiteLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-success");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        const string scenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: scenePath,
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, scenePath),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, scenePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(scenePath, result.Value.ScenePath);
        Assert.NotNull(result.Value.Roots);
        Assert.Single(result.Value.Roots);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSceneTreeLiteLookup_ReturnsReadIndexFormatInvalid_WhenScenePathDoesNotMatchRequestedScene ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-mismatch");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        const string requestedScenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Other.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, requestedScenePath),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, requestedScenePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }
}
