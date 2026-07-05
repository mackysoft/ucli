using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexSceneTreeLiteLookupJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsSceneTreeRootsAndChildrenState ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-tree-lite-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);

        var json = new IndexSceneTreeLiteLookupJsonContractWriter().Write(contract);
        var deserialized = IndexSceneTreeLiteLookupJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.ScenePath, deserialized.ScenePath);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Roots);
        Assert.Single(deserialized.Roots);
        Assert.Equal("Root", deserialized.Roots[0].Name);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.Complete, deserialized.Roots[0].ChildrenState);
        Assert.Single(deserialized.Roots[0].Children!);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.Complete, deserialized.Roots[0].Children![0].ChildrenState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_PreservesSceneRootOrderAndUsesStableContractFields ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Main.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "RootZ",
                    globalObjectId: "z",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "RootA",
                    globalObjectId: "a",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "scenePath": "Assets/Scenes/Main.unity",
                  "sourceInputsHash": "scene-hash",
                  "roots": [
                    {
                      "name": "RootZ",
                      "globalObjectId": "z",
                      "children": [],
                      "childrenState": "complete"
                    },
                    {
                      "name": "RootA",
                      "globalObjectId": "a",
                      "children": [],
                      "childrenState": "complete"
                    }
                  ]
                }
                """),
            new IndexSceneTreeLiteLookupJsonContractWriter().Write(contract));
    }
}
