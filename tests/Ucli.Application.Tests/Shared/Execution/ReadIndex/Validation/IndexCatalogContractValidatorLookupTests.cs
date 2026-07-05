namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorLookupTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsTrue_WhenContractIsComplete ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
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

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsTrue_WhenNodeNameIsWhitespace ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: " ",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenChildCollectionIsMissing ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: null,
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenNodeStateIsMissing ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: null),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenNodeStateIsTruncatedByWindow ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.TruncatedByWindow),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenContractIsComplete ()
    {
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

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenAssetGuidIsDuplicated ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
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
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/B.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "B",
                    TypeId: "B.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "B.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenAssetGuidIsEmpty ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: string.Empty,
                    Name: "A",
                    TypeId: "A.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "A.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/B.asset",
                    AssetGuid: string.Empty,
                    Name: "B",
                    TypeId: "B.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "B.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenNameOrTypeIdIsMissing ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "",
                    TypeId: null,
                    SearchTypeIds:
                    [
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidGuidPathLookup_ReturnsFalse_WhenAssetPathIsDuplicated ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "guid-path-hash",
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/Data/Spawner.asset"),
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "22222222222222222222222222222222",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var result = IndexCatalogContractValidator.IsValidGuidPathLookup(contract);

        Assert.False(result);
    }
}
