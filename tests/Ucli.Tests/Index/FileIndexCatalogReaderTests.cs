using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Index;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileIndexCatalogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTypesCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "types-success");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
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
        WriteText(UcliStoragePathResolver.ResolveTypesCatalogPath(scope.FullPath, fingerprint), IndexTypesCatalogJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadTypesCatalog(scope.FullPath, fingerprint, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexBootstrapFailed_WhenCatalogDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-missing");
        var reader = new FileIndexCatalogReader();

        var result = await reader.ReadSchemasCatalog(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexFormatInvalid_WhenCatalogJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-malformed-json");
        var reader = new FileIndexCatalogReader();
        var catalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(scope.FullPath, "fingerprint");
        WriteText(catalogPath, "{");

        var result = await reader.ReadSchemasCatalog(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadInputsManifest_ReturnsReadIndexFormatInvalid_WhenContractIsIncomplete ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "inputs-incomplete-contract");
        var reader = new FileIndexCatalogReader();
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        WriteText(
            manifestPath,
            """
            {
              "schemaVersion": 1,
              "generatedAtUtc": "2026-03-03T00:00:00+00:00",
              "scriptAssembliesHash": "hash",
              "packagesManifestHash": null,
              "packagesLockHash": "hash",
              "assemblyDefinitionHash": "hash",
              "combinedHash": "hash"
            }
            """);

        var result = await reader.ReadInputsManifest(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    private static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents);
    }
}