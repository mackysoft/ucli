namespace MackySoft.Ucli.Application.Tests;

internal static class ReadyReadIndexArtifactReaderFactory
{
    private static readonly DateTimeOffset GeneratedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");

    public static RecordingReadIndexArtifactReader CreateReadyArtifacts (string? missingArtifactName = null)
    {
        return new RecordingReadIndexArtifactReader
        {
            OpsCatalogResult = CreateResult(
                "ops.catalog",
                missingArtifactName,
                "ops.catalog.json",
                new IndexOpsCatalogJsonContract(1, GeneratedAtUtc, "source-hash", [])),
            AssetSearchLookupResult = CreateResult(
                "asset-search.lookup",
                missingArtifactName,
                "lookups/asset-search.lookup.json",
                new IndexAssetSearchLookupJsonContract(1, GeneratedAtUtc, "source-hash", [])),
            GuidPathLookupResult = CreateResult(
                "guid-path.lookup",
                missingArtifactName,
                "lookups/guid-path.lookup.json",
                new IndexGuidPathLookupJsonContract(1, GeneratedAtUtc, "source-hash", [])),
        };
    }

    private static ReadIndexArtifactReadResult<T> CreateResult<T> (
        string artifactName,
        string? missingArtifactName,
        string artifactPath,
        T contract)
        where T : class
    {
        if (string.Equals(missingArtifactName, artifactName, StringComparison.Ordinal))
        {
            return ReadIndexArtifactReadResult<T>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file '{artifactPath}' does not exist.");
        }

        return ReadIndexArtifactReadResult<T>.Success(contract);
    }
}
