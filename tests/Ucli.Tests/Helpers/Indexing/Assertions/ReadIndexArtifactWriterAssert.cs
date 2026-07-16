namespace MackySoft.Ucli.Tests.Helpers.Indexing;

internal static class ReadIndexArtifactWriterAssert
{
    public static RecordingReadIndexArtifactWriter.OpsCatalogInvocation OpsCatalogWritten (
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedSourceInputsHash)
    {
        var invocation = Assert.Single(artifactWriter.OpsCatalogInvocations);
        Assert.Equal(Sha256DigestTestFactory.Compute(expectedSourceInputsHash), invocation.SourceInputsHash);
        return invocation;
    }

    public static RecordingReadIndexArtifactWriter.OpsCatalogInvocation OpsCatalogWrittenWithManifestInputSnapshot (
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedSourceInputsHash,
        string? expectedAssetsContentHash = null,
        string? expectedAssetSearchHash = null,
        string? expectedGuidPathHash = null,
        string? expectedCombinedHash = null)
    {
        var invocation = OpsCatalogWritten(artifactWriter, expectedSourceInputsHash);
        Assert.NotNull(invocation.ManifestInputSnapshot);
        var snapshot = invocation.ManifestInputSnapshot!;
        if (expectedAssetsContentHash is not null)
        {
            Assert.Equal(Sha256DigestTestFactory.Compute(expectedAssetsContentHash), snapshot.AssetsContentHash);
        }

        if (expectedAssetSearchHash is not null)
        {
            Assert.Equal(Sha256DigestTestFactory.Compute(expectedAssetSearchHash), snapshot.AssetSearchHash);
        }

        if (expectedGuidPathHash is not null)
        {
            Assert.Equal(Sha256DigestTestFactory.Compute(expectedGuidPathHash), snapshot.GuidPathHash);
        }

        if (expectedCombinedHash is not null)
        {
            Assert.Equal(Sha256DigestTestFactory.Compute(expectedCombinedHash), snapshot.CombinedHash);
        }

        return invocation;
    }

    public static RecordingReadIndexArtifactWriter.AssetLookupInvocation AssetLookupWritten (
        RecordingReadIndexArtifactWriter artifactWriter,
        DateTimeOffset expectedGeneratedAtUtc,
        ReadIndexInputHashSnapshot expectedInputSnapshot)
    {
        var invocation = Assert.Single(artifactWriter.AssetLookupInvocations);
        Assert.Equal(expectedGeneratedAtUtc, invocation.GeneratedAtUtc);
        Assert.Same(expectedInputSnapshot, invocation.InputSnapshot);
        return invocation;
    }

    public static RecordingReadIndexArtifactWriter.AssetLookupInvocation AssetLookupWrittenWithInputSnapshot (
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedAssetSearchHash,
        string expectedAssetPath)
    {
        var invocation = Assert.Single(artifactWriter.AssetLookupInvocations);
        Assert.Equal(Sha256DigestTestFactory.Compute(expectedAssetSearchHash), invocation.InputSnapshot.AssetSearchHash);
        Assert.Equal(expectedAssetPath, invocation.AssetSearchEntries[0].AssetPath);
        return invocation;
    }

    public static RecordingReadIndexArtifactWriter.SceneTreeLiteInvocation SceneTreeLiteWritten (
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedSourceInputsHash)
    {
        var invocation = Assert.Single(artifactWriter.SceneTreeLiteInvocations);
        Assert.Equal(Sha256DigestTestFactory.Compute(expectedSourceInputsHash), invocation.SourceInputsHash);
        return invocation;
    }

}
