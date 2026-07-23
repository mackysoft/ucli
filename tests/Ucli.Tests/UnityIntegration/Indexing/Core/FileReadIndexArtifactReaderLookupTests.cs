using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderLookupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAssetSearchLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "asset-search-success");
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(AbsolutePath.Parse(scope.FullPath), fingerprint);
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("asset-search-hash").ToString(),
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
            UcliStoragePathResolver.ResolveAssetSearchLookupPath(AbsolutePath.Parse(scope.FullPath), fingerprint, generationId),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadAssetSearchLookupAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(Sha256DigestTestFactory.Compute("asset-search-hash"), result.Value.SourceInputsHash);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadGuidPathLookup_ReturnsReadIndexFormatInvalid_WhenLookupJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "guid-path-malformed");
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(AbsolutePath.Parse(scope.FullPath), fingerprint);
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var lookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(AbsolutePath.Parse(scope.FullPath), fingerprint, generationId);
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
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        const string scenePath = "Assets/Scenes/Sample.unity";
        var typedScenePath = new SceneAssetPath(scenePath);
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: scenePath,
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-11111111111111111111111111111111-4-5",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
                AbsolutePath.Parse(scope.FullPath),
                fingerprint,
                new SceneAssetPath(scenePath)),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, typedScenePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(typedScenePath, result.Value.ScenePath);
        Assert.NotNull(result.Value.Roots);
        Assert.Single(result.Value.Roots);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSceneTreeLiteLookup_ReturnsReadIndexFormatInvalid_WhenScenePathDoesNotMatchRequestedScene ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-mismatch");
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        const string requestedScenePath = "Assets/Scenes/Sample.unity";
        var typedRequestedScenePath = new SceneAssetPath(requestedScenePath);
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Other.unity",
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-11111111111111111111111111111111-4-5",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
                AbsolutePath.Parse(scope.FullPath),
                fingerprint,
                new SceneAssetPath(requestedScenePath)),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, typedRequestedScenePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }
}
