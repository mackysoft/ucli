using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderCatalogManifestTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadInputsManifest_ReturnsReadIndexFormatInvalid_WhenContractIsIncomplete ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "inputs-incomplete-contract");
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        FileReadIndexArtifactReaderTestSupport.WriteText(
            manifestPath,
            """
            {
              "schemaVersion": 1,
              "generatedAtUtc": "2026-03-03T00:00:00+00:00",
              "scriptAssembliesHash": "hash",
              "packagesManifestHash": null,
              "packagesLockHash": "hash",
              "assemblyDefinitionHash": "hash",
              "assetsContentHash": "hash",
              "assetSearchHash": "hash",
              "guidPathHash": "hash",
              "combinedHash": "hash"
            }
            """);

        var result = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }
}
