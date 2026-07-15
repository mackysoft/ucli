namespace MackySoft.Ucli.Application.Tests.Shared.Execution.ReadIndex.Artifacts;

public sealed class ReadIndexInputsManifestSnapshotTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithCanonicalContract_ProjectsHashesOnce ()
    {
        var contract = CreateContract();

        var result = ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
        Assert.Equal(contract.GeneratedAtUtc, snapshot.GeneratedAtUtc);
        Assert.Equal(Sha256DigestTestFactory.Create('0'), snapshot.Hashes.ScriptAssembliesHash);
        Assert.Equal(Sha256DigestTestFactory.Create('7'), snapshot.Hashes.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithNonUtcGeneratedAt_ReturnsFalse ()
    {
        var contract = CreateContract() with
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-07-14T09:00:00+09:00"),
        };

        var result = ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithMissingCombinedHash_ReturnsFalse ()
    {
        var contract = CreateContract() with
        {
            CombinedHash = null,
        };

        var result = ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Theory]
    [InlineData("scriptAssembliesHash")]
    [InlineData("packagesManifestHash")]
    [InlineData("packagesLockHash")]
    [InlineData("assemblyDefinitionHash")]
    [InlineData("assetsContentHash")]
    [InlineData("assetSearchHash")]
    [InlineData("guidPathHash")]
    [InlineData("combinedHash")]
    [Trait("Size", "Small")]
    public void TryCreate_WithNonCanonicalHash_ReturnsFalse (string hashProperty)
    {
        var validDigest = Sha256DigestTestFactory.Compute("valid").ToString();
        var contract = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: ResolveHash("scriptAssembliesHash"),
            PackagesManifestHash: ResolveHash("packagesManifestHash"),
            PackagesLockHash: ResolveHash("packagesLockHash"),
            AssemblyDefinitionHash: ResolveHash("assemblyDefinitionHash"),
            AssetsContentHash: ResolveHash("assetsContentHash"),
            AssetSearchHash: ResolveHash("assetSearchHash"),
            GuidPathHash: ResolveHash("guidPathHash"),
            CombinedHash: ResolveHash("combinedHash"));

        var result = ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);

        string ResolveHash (string propertyName)
        {
            return string.Equals(propertyName, hashProperty, StringComparison.Ordinal)
                ? "not-a-digest"
                : validDigest;
        }
    }

    private static IndexInputsManifestJsonContract CreateContract ()
    {
        return new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-07-14T00:00:00+00:00"),
            ScriptAssembliesHash: new string('0', 64),
            PackagesManifestHash: new string('1', 64),
            PackagesLockHash: new string('2', 64),
            AssemblyDefinitionHash: new string('3', 64),
            AssetsContentHash: new string('4', 64),
            AssetSearchHash: new string('5', 64),
            GuidPathHash: new string('6', 64),
            CombinedHash: new string('7', 64));
    }
}
