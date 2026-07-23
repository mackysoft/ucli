using MackySoft.FileSystem;
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

        var absoluteUnityProjectPath = AbsolutePath.Parse(unityProjectPath);
        var fingerprint = UnityProjectFingerprintCalculator.Create(
            absoluteUnityProjectPath,
            absoluteUnityProjectPath);
        var writer = new FileReadIndexArtifactWriter(
            new IndexOpsCatalogJsonContractWriter(),
            new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter(),
            new FileReadIndexGenerationStore(
                new FileReadIndexGenerationPointerStore(),
                TimeProvider.System));
        writer.WriteOpsCatalogAsync(
                absoluteUnityProjectPath,
                fingerprint,
                DefaultGeneratedAtUtc,
                OperationCatalogTestFixtures.CreateSnapshot(DefaultGeneratedAtUtc, operations).Operations,
                Sha256DigestTestFactory.Compute(DefaultSourceInputsHash),
                manifestInputSnapshot: null,
                CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
