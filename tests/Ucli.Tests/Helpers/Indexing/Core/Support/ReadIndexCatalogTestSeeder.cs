using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Tests;

internal static class ReadIndexCatalogTestSeeder
{
    private const string DefaultSourceInputsHash = "source-hash";

    private static readonly DateTimeOffset DefaultGeneratedAtUtc = DateTimeOffset.Parse("2026-03-06T00:00:00+00:00");

    public static void SeedOpsCatalog (
        string unityProjectPath,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectPath);
        ArgumentNullException.ThrowIfNull(operations);

        var fingerprint = UnityProjectFingerprintCalculator.Create(unityProjectPath, unityProjectPath);
        var writer = new FileReadIndexArtifactWriter(
            new IndexOpsCatalogJsonContractWriter(),
            new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter());
        writer.WriteOpsCatalogAsync(
                unityProjectPath,
                fingerprint,
                DefaultGeneratedAtUtc,
                operations,
                DefaultSourceInputsHash,
                manifestInputSnapshot: null,
                CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
