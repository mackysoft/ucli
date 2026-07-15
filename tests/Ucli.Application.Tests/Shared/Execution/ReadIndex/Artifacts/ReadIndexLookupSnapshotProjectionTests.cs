using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution.ReadIndex.Artifacts;

public sealed class ReadIndexLookupSnapshotProjectionTests
{
    private const string AssetGuidText = "11111111111111111111111111111111";
    private const string GlobalObjectIdText = $"GlobalObjectId_V1-1-{AssetGuidText}-1-0";

    [Fact]
    [Trait("Size", "Small")]
    public void AssetSearchLookupSnapshot_TryCreate_ProjectsSemanticEntryValues ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-07-14T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Create('1').ToString(),
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: AssetGuidText,
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds: ["Game.Spawner, Assembly-CSharp"]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Unimported.asset",
                    AssetGuid: string.Empty,
                    Name: "Unimported",
                    TypeId: "Game.Unimported, Assembly-CSharp",
                    SearchTypeIds: ["Game.Unimported, Assembly-CSharp"]),
            ]);

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
        Assert.Equal(new UnityAssetPath("Assets/Data/Spawner.asset"), snapshot.Entries[0].AssetPath);
        Assert.Equal(Guid.ParseExact(AssetGuidText, "N"), snapshot.Entries[0].AssetGuid);
        Assert.Null(snapshot.Entries[1].AssetGuid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GuidPathLookupSnapshot_TryCreate_ProjectsSemanticEntryValues ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-07-14T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Create('2').ToString(),
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: AssetGuidText,
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var result = GuidPathLookupSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
        Assert.Equal(Guid.ParseExact(AssetGuidText, "N"), snapshot.Entries[0].AssetGuid);
        Assert.Equal(new UnityAssetPath("Assets/Data/Spawner.asset"), snapshot.Entries[0].AssetPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneTreeLiteLookupSnapshot_TryCreate_ProjectsSemanticNodeValues ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-07-14T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Main.unity",
            SourceInputsHash: Sha256DigestTestFactory.Create('3').ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: GlobalObjectIdText,
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: [],
                            childrenState: IndexSceneTreeLiteNodeChildrenState.Unknown),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
        Assert.Equal(new SceneAssetPath("Assets/Scenes/Main.unity"), snapshot.ScenePath);
        Assert.Equal(new UnityGlobalObjectId(GlobalObjectIdText), snapshot.Roots[0].GlobalObjectId);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenState.Complete, snapshot.Roots[0].ChildrenState);
        Assert.Null(snapshot.Roots[0].Children[0].GlobalObjectId);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenState.Unknown, snapshot.Roots[0].Children[0].ChildrenState);
    }
}
