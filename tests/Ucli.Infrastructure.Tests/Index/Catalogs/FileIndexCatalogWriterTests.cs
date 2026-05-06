using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Catalogs;

public sealed class FileIndexCatalogWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenInputsAreValid_CreatesExpectedCatalogPaths ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-catalog-writer", "success");
        var typesCatalogWriter = new IndexTypesCatalogJsonContractWriter();
        var schemasCatalogWriter = new IndexSchemasCatalogJsonContractWriter();
        var inputsManifestWriter = new IndexInputsManifestJsonContractWriter();
        var writer = new FileIndexCatalogWriter(
            typesCatalogWriter,
            schemasCatalogWriter,
            inputsManifestWriter);
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-04T00:00:00+00:00");
        var typesCatalog = new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: "combined-hash",
            Entries: Array.Empty<IndexTypeEntryJsonContract>());
        var schemasCatalog = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: "combined-hash",
            Entries: Array.Empty<IndexSchemaEntryJsonContract>());
        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            AssetsContentHash: "assets-hash",
            AssetSearchHash: "asset-search-hash",
            GuidPathHash: "guid-path-hash",
            CombinedHash: "combined-hash");

        var result = await writer.Write(
            scope.FullPath,
            "writer-fingerprint",
            typesCatalog,
            schemasCatalog,
            inputsManifest,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        var typesCatalogPath = UcliStoragePathResolver.ResolveTypesCatalogPath(scope.FullPath, "writer-fingerprint");
        var schemasCatalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(scope.FullPath, "writer-fingerprint");
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "writer-fingerprint");

        Assert.True(File.Exists(typesCatalogPath));
        Assert.True(File.Exists(schemasCatalogPath));
        Assert.True(File.Exists(inputsManifestPath));
        Assert.Equal(typesCatalogWriter.Write(typesCatalog), await File.ReadAllTextAsync(typesCatalogPath, CancellationToken.None));
        Assert.Equal(schemasCatalogWriter.Write(schemasCatalog), await File.ReadAllTextAsync(schemasCatalogPath, CancellationToken.None));
        Assert.Equal(inputsManifestWriter.Write(inputsManifest), await File.ReadAllTextAsync(inputsManifestPath, CancellationToken.None));
    }
}
