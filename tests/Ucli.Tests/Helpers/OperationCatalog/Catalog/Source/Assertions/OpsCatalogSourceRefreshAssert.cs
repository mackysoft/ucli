using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Tests.Helpers.Indexing;

namespace MackySoft.Ucli.Tests.Helpers.OperationCatalog;

internal static class OpsCatalogSourceRefreshAssert
{
    public static void PersistedWithFullInputSnapshot (
        OpsCatalogSourceRefreshResult result,
        RecordingReadIndexInputFingerprintProvider fingerprintProvider,
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedFallbackReason,
        string expectedSourceInputsHash,
        string expectedAssetSearchHash)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedFallbackReason, result.FallbackReason);
        ReadIndexInputFingerprintAssert.CoreFingerprintRequested(fingerprintProvider);
        Assert.Single(fingerprintProvider.FullInvocations);
        ReadIndexArtifactWriterAssert.OpsCatalogWrittenWithManifestInputSnapshot(
            artifactWriter,
            expectedSourceInputsHash,
            expectedAssetSearchHash: expectedAssetSearchHash);
    }

    public static void PersistedWithReusedManifestAssetHashes (
        OpsCatalogSourceRefreshResult result,
        RecordingReadIndexInputFingerprintProvider fingerprintProvider,
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedSourceInputsHash,
        string expectedAssetsContentHash,
        string expectedAssetSearchHash,
        string expectedGuidPathHash,
        string expectedCombinedHash)
    {
        Assert.True(result.IsSuccess);
        ReadIndexInputFingerprintAssert.CoreFingerprintRequested(fingerprintProvider);
        Assert.Empty(fingerprintProvider.FullInvocations);
        ReadIndexArtifactWriterAssert.OpsCatalogWrittenWithManifestInputSnapshot(
            artifactWriter,
            expectedSourceInputsHash,
            expectedAssetsContentHash: expectedAssetsContentHash,
            expectedAssetSearchHash: expectedAssetSearchHash,
            expectedGuidPathHash: expectedGuidPathHash,
            expectedCombinedHash: expectedCombinedHash);
    }

    public static void SourceResultReturnedWithFingerprintFailureBeforePersistence (
        OpsCatalogSourceRefreshResult result,
        RecordingOpsCatalogReader reader,
        RecordingReadIndexInputFingerprintProvider fingerprintProvider,
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedFallbackReason)
    {
        Assert.True(result.IsSuccess);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("input fingerprint could not be computed", result.FallbackReason!, StringComparison.Ordinal);
        OpsCatalogReaderAssert.ReadRequiresReadinessGate(reader);
        ReadIndexInputFingerprintAssert.CoreFingerprintRequested(fingerprintProvider);
        Assert.Empty(fingerprintProvider.FullInvocations);
        Assert.Empty(artifactWriter.OpsCatalogInvocations);
    }

    public static void FirstSourceResultReturnedAfterRetryFailureWithoutPersistence (
        OpsCatalogSourceRefreshResult result,
        RecordingReadIndexArtifactWriter artifactWriter,
        string expectedFirstOperationName,
        string expectedFallbackReason,
        string expectedRetryFailureMessage)
    {
        Assert.True(result.IsSuccess);
        Assert.Single(result.Snapshot!.Operations);
        Assert.Equal(expectedFirstOperationName, result.Snapshot.Operations[0].Name);
        Assert.Contains(expectedFallbackReason, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("project inputs changed while the catalog was being read", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains(expectedRetryFailureMessage, result.FallbackReason!, StringComparison.Ordinal);
        Assert.Empty(artifactWriter.OpsCatalogInvocations);
    }
}
