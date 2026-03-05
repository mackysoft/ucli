using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Index;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexFreshnessPolicyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsFresh_WhenManifestMatchesSnapshot ()
    {
        var snapshot = CreateSnapshot();
        var manifest = CreateManifest(snapshot);

        var result = IndexFreshnessPolicy.EvaluateFreshness(manifest, snapshot);

        Assert.Equal(IndexFreshness.Fresh, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvaluateFreshness_ReturnsStale_WhenManifestDoesNotMatchSnapshot ()
    {
        var snapshot = CreateSnapshot();
        var manifest = CreateManifest(snapshot with { PackagesLockHash = "different-lock-hash" });

        var result = IndexFreshnessPolicy.EvaluateFreshness(manifest, snapshot);

        Assert.Equal(IndexFreshness.Stale, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsFailure_WhenRequireFreshAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.RequireFresh, IndexFreshness.Stale);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsSuccess_WhenAllowStaleAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.AllowStale, IndexFreshness.Stale);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Null(result.Error);
    }

    private static IndexInputHashSnapshot CreateSnapshot ()
    {
        return new IndexInputHashSnapshot(
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            CombinedHash: "combined-hash");
    }

    private static IndexInputsManifestJsonContract CreateManifest (IndexInputHashSnapshot snapshot)
    {
        return new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: snapshot.ScriptAssembliesHash,
            PackagesManifestHash: snapshot.PackagesManifestHash,
            PackagesLockHash: snapshot.PackagesLockHash,
            AssemblyDefinitionHash: snapshot.AssemblyDefinitionHash,
            CombinedHash: snapshot.CombinedHash);
    }
}