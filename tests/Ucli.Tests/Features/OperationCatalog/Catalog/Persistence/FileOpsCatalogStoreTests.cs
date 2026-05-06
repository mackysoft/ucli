using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Persistence;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Persistence;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.OperationCatalog.Catalog.Persistence;

public sealed class FileOpsCatalogStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WithManifest_WritesCatalogAndManifestWithWriterOutput ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-catalog-store", "write-with-manifest");
        var opsCatalogWriter = new RecordingJsonContractWriter<IndexOpsCatalogJsonContract>("ops writer output\n");
        var inputsManifestWriter = new RecordingJsonContractWriter<IndexInputsManifestJsonContract>("manifest writer output\n");
        var store = new FileOpsCatalogStore(opsCatalogWriter, inputsManifestWriter);
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            new IndexOpEntryJsonContract(
                Name: "z.op",
                Kind: "mutation",
                Policy: "dangerous",
                ArgsSchemaJson: """{"type":"object"}"""),
            new IndexOpEntryJsonContract(
                Name: "a.op",
                Kind: "query",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}"""),
        ];
        var inputSnapshot = new OpsCatalogInputHashSnapshot(
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            AssetsContentHash: "assets",
            AssetSearchHash: "asset-search",
            GuidPathHash: "guid-path",
            CombinedHash: "combined");

        await store.Write(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            inputSnapshot);

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, "fingerprint");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");

        Assert.True(File.Exists(opsCatalogPath));
        Assert.True(File.Exists(manifestPath));
        Assert.Equal("ops writer output\n", await File.ReadAllTextAsync(opsCatalogPath, CancellationToken.None));
        Assert.Equal("manifest writer output\n", await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
        Assert.Equal(1, opsCatalogWriter.CallCount);
        Assert.Equal(1, inputsManifestWriter.CallCount);

        var opsContract = opsCatalogWriter.LastContract;
        Assert.NotNull(opsContract);
        Assert.Equal(1, opsContract!.SchemaVersion);
        Assert.Equal(generatedAtUtc, opsContract.GeneratedAtUtc);
        Assert.Equal("ops-hash", opsContract.SourceInputsHash);
        Assert.Collection(
            opsContract.Entries!,
            static entry => Assert.Equal("z.op", entry.Name),
            static entry => Assert.Equal("a.op", entry.Name));

        var manifestContract = inputsManifestWriter.LastContract;
        Assert.NotNull(manifestContract);
        Assert.Equal(1, manifestContract!.SchemaVersion);
        Assert.Equal(generatedAtUtc, manifestContract.GeneratedAtUtc);
        Assert.Equal(inputSnapshot.ScriptAssembliesHash, manifestContract.ScriptAssembliesHash);
        Assert.Equal(inputSnapshot.PackagesManifestHash, manifestContract.PackagesManifestHash);
        Assert.Equal(inputSnapshot.PackagesLockHash, manifestContract.PackagesLockHash);
        Assert.Equal(inputSnapshot.AssemblyDefinitionHash, manifestContract.AssemblyDefinitionHash);
        Assert.Equal(inputSnapshot.AssetsContentHash, manifestContract.AssetsContentHash);
        Assert.Equal(inputSnapshot.AssetSearchHash, manifestContract.AssetSearchHash);
        Assert.Equal(inputSnapshot.GuidPathHash, manifestContract.GuidPathHash);
        Assert.Equal(inputSnapshot.CombinedHash, manifestContract.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WithoutManifest_WritesCatalogOnly ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-catalog-store", "write-without-manifest");
        var opsCatalogWriter = new RecordingJsonContractWriter<IndexOpsCatalogJsonContract>("ops writer output\n");
        var inputsManifestWriter = new RecordingJsonContractWriter<IndexInputsManifestJsonContract>("manifest writer output\n");
        var store = new FileOpsCatalogStore(opsCatalogWriter, inputsManifestWriter);
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            new IndexOpEntryJsonContract(
                Name: "query.op",
                Kind: "query",
                Policy: "safe",
                ArgsSchemaJson: """{"type":"object"}"""),
        ];

        await store.Write(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            manifestInputSnapshot: null);

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, "fingerprint");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");

        Assert.True(File.Exists(opsCatalogPath));
        Assert.False(File.Exists(manifestPath));
        Assert.Equal("ops writer output\n", await File.ReadAllTextAsync(opsCatalogPath, CancellationToken.None));
        Assert.Equal(1, opsCatalogWriter.CallCount);
        Assert.Equal(0, inputsManifestWriter.CallCount);

        var opsContract = opsCatalogWriter.LastContract;
        Assert.NotNull(opsContract);
        Assert.Equal(1, opsContract!.SchemaVersion);
        Assert.Equal(generatedAtUtc, opsContract.GeneratedAtUtc);
        Assert.Equal("ops-hash", opsContract.SourceInputsHash);
        var opEntry = Assert.Single(opsContract.Entries!);
        Assert.Equal("query.op", opEntry.Name);
    }

    private sealed class RecordingJsonContractWriter<TContract> : IJsonContractWriter<TContract>
    {
        private readonly string output;

        public RecordingJsonContractWriter (string output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public int CallCount { get; private set; }

        public TContract? LastContract { get; private set; }

        public string Write (TContract contract)
        {
            CallCount++;
            LastContract = contract;
            return output;
        }
    }
}
