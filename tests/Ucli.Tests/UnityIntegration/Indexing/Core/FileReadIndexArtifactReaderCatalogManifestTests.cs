using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderCatalogManifestTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadTypesCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "types-success");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var contract = new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexTypeEntryJsonContract(
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Namespace: "Game",
                    AssemblyName: "Assembly-CSharp",
                    BaseTypeId: "UnityEngine.MonoBehaviour, UnityEngine.CoreModule",
                    Flags: new IndexTypeFlagsJsonContract(
                        IsAbstract: false,
                        IsGenericDefinition: false,
                        IsUnityObject: true,
                        IsComponent: true,
                        IsScriptableObject: false,
                        IsSerializeReferenceCandidate: false)),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveTypesCatalogPath(scope.FullPath, fingerprint),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadTypesCatalogAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexBootstrapFailed_WhenCatalogDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-missing");
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));

        var result = await reader.ReadSchemasCatalogAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexFormatInvalid_WhenCatalogJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-malformed-json");
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var catalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        FileReadIndexArtifactReaderTestSupport.WriteText(catalogPath, "{");

        var result = await reader.ReadSchemasCatalogAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

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
