namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorTests
{
    [Theory]
    [InlineData("ops.catalog")]
    [InlineData("ops.describe")]
    [InlineData("asset-search.lookup")]
    [InlineData("guid-path.lookup")]
    [InlineData("scene-tree-lite.lookup")]
    [Trait("Size", "Small")]
    public void CatalogValidator_ReturnsFalse_WhenSourceInputsHashIsNotCanonical (string artifactName)
    {
        const string invalidDigest = "not-a-digest";
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-03T00:00:00+00:00");

        var result = artifactName switch
        {
            "ops.catalog" => OpsCatalogDescriptorSnapshot.TryCreate(
                new IndexOpsCatalogJsonContract(1, generatedAtUtc, invalidDigest, []),
                out _),
            "ops.describe" => OpsDescribeSnapshot.TryCreate(
                IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
                    IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry()) with
                {
                    SourceInputsHash = invalidDigest,
                },
                out _),
            "asset-search.lookup" => AssetSearchLookupSnapshot.TryCreate(
                new IndexAssetSearchLookupJsonContract(1, generatedAtUtc, invalidDigest, []),
                out _),
            "guid-path.lookup" => GuidPathLookupSnapshot.TryCreate(
                new IndexGuidPathLookupJsonContract(1, generatedAtUtc, invalidDigest, []),
                out _),
            "scene-tree-lite.lookup" => SceneTreeLiteLookupSnapshot.TryCreate(
                new IndexSceneTreeLiteLookupJsonContract(1, generatedAtUtc, "Assets/Scenes/Sample.unity", invalidDigest, []),
                out _),
            _ => throw new ArgumentOutOfRangeException(nameof(artifactName), artifactName, "Unsupported test artifact."),
        };

        Assert.False(result);
    }

    [Theory]
    [InlineData("ops.catalog")]
    [InlineData("ops.describe")]
    [InlineData("asset-search.lookup")]
    [InlineData("guid-path.lookup")]
    [InlineData("scene-tree-lite.lookup")]
    [Trait("Size", "Small")]
    public void CatalogValidator_ReturnsFalse_WhenGeneratedAtUtcIsNotUtc (string artifactName)
    {
        var sourceInputsHash = Sha256DigestTestFactory.Compute("source-hash").ToString();
        DateTimeOffset[] invalidTimestamps =
        {
            default,
            DateTimeOffset.Parse("2026-03-03T09:00:00+09:00"),
        };

        foreach (var invalidTimestamp in invalidTimestamps)
        {
            var result = artifactName switch
            {
                "ops.catalog" => OpsCatalogDescriptorSnapshot.TryCreate(
                    new IndexOpsCatalogJsonContract(1, invalidTimestamp, sourceInputsHash, []),
                    out _),
                "ops.describe" => OpsDescribeSnapshot.TryCreate(
                    IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
                        IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry()) with
                    {
                        GeneratedAtUtc = invalidTimestamp,
                    },
                    out _),
                "asset-search.lookup" => AssetSearchLookupSnapshot.TryCreate(
                    new IndexAssetSearchLookupJsonContract(1, invalidTimestamp, sourceInputsHash, []),
                    out _),
                "guid-path.lookup" => GuidPathLookupSnapshot.TryCreate(
                    new IndexGuidPathLookupJsonContract(1, invalidTimestamp, sourceInputsHash, []),
                    out _),
                "scene-tree-lite.lookup" => SceneTreeLiteLookupSnapshot.TryCreate(
                    new IndexSceneTreeLiteLookupJsonContract(1, invalidTimestamp, "Assets/Scenes/Sample.unity", sourceInputsHash, []),
                    out _),
                _ => throw new ArgumentOutOfRangeException(nameof(artifactName), artifactName, "Unsupported test artifact."),
            };

            Assert.False(result);
        }
    }

}
