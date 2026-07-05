using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexInputsManifestJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsManifestHashes ()
    {
        var contract = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: "assemblies-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            AssetsContentHash: "assets-hash",
            AssetSearchHash: "asset-search-hash",
            GuidPathHash: "guid-path-hash",
            CombinedHash: "combined-hash");

        var json = new IndexInputsManifestJsonContractWriter().Write(contract);
        var deserialized = IndexInputsManifestJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.ScriptAssembliesHash, deserialized.ScriptAssembliesHash);
        Assert.Equal(contract.PackagesManifestHash, deserialized.PackagesManifestHash);
        Assert.Equal(contract.PackagesLockHash, deserialized.PackagesLockHash);
        Assert.Equal(contract.AssemblyDefinitionHash, deserialized.AssemblyDefinitionHash);
        Assert.Equal(contract.AssetsContentHash, deserialized.AssetsContentHash);
        Assert.Equal(contract.AssetSearchHash, deserialized.AssetSearchHash);
        Assert.Equal(contract.GuidPathHash, deserialized.GuidPathHash);
        Assert.Equal(contract.CombinedHash, deserialized.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_UsesStableManifestHashFields ()
    {
        var contract = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            AssetsContentHash: "assets",
            AssetSearchHash: "asset",
            GuidPathHash: "guid",
            CombinedHash: "combined");

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "scriptAssembliesHash": "script",
                  "packagesManifestHash": "manifest",
                  "packagesLockHash": "lock",
                  "assemblyDefinitionHash": "asmdef",
                  "assetsContentHash": "assets",
                  "assetSearchHash": "asset",
                  "guidPathHash": "guid",
                  "combinedHash": "combined"
                }
                """),
            new IndexInputsManifestJsonContractWriter().Write(contract));
    }
}
