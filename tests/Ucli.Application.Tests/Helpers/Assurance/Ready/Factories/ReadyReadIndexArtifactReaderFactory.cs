using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests;

internal static class ReadyReadIndexArtifactReaderFactory
{
    private static readonly DateTimeOffset GeneratedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");

    private static readonly Sha256Digest SourceInputsHash = Sha256DigestTestFactory.Compute("source-hash");

    public static RecordingReadIndexArtifactReader CreateReadyArtifacts (string? missingArtifactName = null)
    {
        return new RecordingReadIndexArtifactReader
        {
            OpsCatalogResult = CreateResult(
                "ops.catalog",
                missingArtifactName,
                "ops.catalog.json",
                CreateOpsCatalogSnapshot()),
            AssetSearchLookupResult = CreateResult(
                "asset-search.lookup",
                missingArtifactName,
                "lookups/asset-search.lookup.json",
                CreateAssetSearchLookupSnapshot()),
            GuidPathLookupResult = CreateResult(
                "guid-path.lookup",
                missingArtifactName,
                "lookups/guid-path.lookup.json",
                CreateGuidPathLookupSnapshot()),
        };
    }

    private static OpsCatalogDescriptorSnapshot CreateOpsCatalogSnapshot ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            1,
            GeneratedAtUtc,
            SourceInputsHash.ToString(),
            []);
        if (!OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot))
        {
            throw new InvalidOperationException("Ready ops-catalog fixture is invalid.");
        }

        return snapshot;
    }

    private static AssetSearchLookupSnapshot CreateAssetSearchLookupSnapshot ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            1,
            GeneratedAtUtc,
            SourceInputsHash.ToString(),
            []);
        if (!AssetSearchLookupSnapshot.TryCreate(contract, out var snapshot))
        {
            throw new InvalidOperationException("Ready asset-search fixture is invalid.");
        }

        return snapshot;
    }

    private static GuidPathLookupSnapshot CreateGuidPathLookupSnapshot ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            1,
            GeneratedAtUtc,
            SourceInputsHash.ToString(),
            []);
        if (!GuidPathLookupSnapshot.TryCreate(contract, out var snapshot))
        {
            throw new InvalidOperationException("Ready GUID-path fixture is invalid.");
        }

        return snapshot;
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
