namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorLookupTests
{
    private const string ValidGlobalObjectId = "GlobalObjectId_V1-2-11111111111111111111111111111111-4-5";

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsTrue_WhenContractIsComplete ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: ValidGlobalObjectId,
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                            childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out _);

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
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: " ",
                    globalObjectId: ValidGlobalObjectId,
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out _);

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
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: ValidGlobalObjectId,
                    children: null,
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out _);

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
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: ValidGlobalObjectId,
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenState.TruncatedByWindow),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenContractIsComplete ()
    {
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

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out _);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenAssetGuidIsDuplicated ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("asset-search-hash").ToString(),
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

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenAssetGuidIsEmpty ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("asset-search-hash").ToString(),
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

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out _);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenNameOrTypeIdIsMissing ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("asset-search-hash").ToString(),
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

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidGuidPathLookup_ReturnsFalse_WhenAssetPathIsDuplicated ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("guid-path-hash").ToString(),
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/Data/Spawner.asset"),
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "22222222222222222222222222222222",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var result = GuidPathLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("not-an-asset-guid")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenNonEmptyAssetGuidIsNotCanonical (string assetGuid)
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("asset-search-hash").ToString(),
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: assetGuid,
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds: ["Game.Spawner, Assembly-CSharp"]),
            ]);

        var result = AssetSearchLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("not-an-asset-guid")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [Trait("Size", "Small")]
    public void IsValidGuidPathLookup_ReturnsFalse_WhenAssetGuidIsNotCanonical (string assetGuid)
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("guid-path-hash").ToString(),
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: assetGuid,
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var result = GuidPathLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenNonEmptyGlobalObjectIdIsInvalid ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: Sha256DigestTestFactory.Compute("scene-hash").ToString(),
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "not-a-global-object-id",
                    children: [],
                    childrenState: IndexSceneTreeLiteNodeChildrenState.Complete),
            ]);

        var result = SceneTreeLiteLookupSnapshot.TryCreate(contract, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("asset-search", "Assets/Data/../Spawner.asset")]
    [InlineData("asset-search", @"Assets\Data\Spawner.asset")]
    [InlineData("guid-path", "Assets/Data/../Spawner.asset")]
    [InlineData("guid-path", @"Assets\Data\Spawner.asset")]
    [Trait("Size", "Small")]
    public void LookupValidator_ReturnsFalse_WhenAssetPathIsNotNormalized (
        string lookupName,
        string assetPath)
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-03T00:00:00+00:00");
        var sourceInputsHash = Sha256DigestTestFactory.Compute("source-hash").ToString();

        var result = lookupName switch
        {
            "asset-search" => AssetSearchLookupSnapshot.TryCreate(
                new IndexAssetSearchLookupJsonContract(
                    1,
                    generatedAtUtc,
                    sourceInputsHash,
                    [new IndexAssetSearchEntryJsonContract(assetPath, "11111111111111111111111111111111", "Spawner", "Game.Spawner", ["Game.Spawner"])]),
                out _),
            "guid-path" => GuidPathLookupSnapshot.TryCreate(
                new IndexGuidPathLookupJsonContract(
                    1,
                    generatedAtUtc,
                    sourceInputsHash,
                    [new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", assetPath)]),
                out _),
            _ => throw new ArgumentOutOfRangeException(nameof(lookupName), lookupName, "Unsupported test lookup."),
        };

        Assert.False(result);
    }
}
