using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Index;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsFresh_WhenSnapshotMatchesInputsManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "fresh");
        PrepareRequiredInputs(scope);
        var hasher = new IndexInputSnapshotHasher();
        var snapshot = await hasher.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        WriteInputsManifest(scope.FullPath, "fingerprint", snapshot!);
        var evaluator = new IndexFreshnessEvaluator(new FileIndexCatalogReader(), hasher);

        var result = await evaluator.Evaluate(
            storageRoot: scope.FullPath,
            projectFingerprint: "fingerprint",
            projectRoot: scope.FullPath,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsStale_WhenSnapshotDiffersFromInputsManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "stale");
        PrepareRequiredInputs(scope);
        var hasher = new IndexInputSnapshotHasher();
        var snapshot = await hasher.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        WriteInputsManifest(scope.FullPath, "fingerprint", snapshot!);
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "updated");
        var evaluator = new IndexFreshnessEvaluator(new FileIndexCatalogReader(), hasher);

        var result = await evaluator.Evaluate(
            storageRoot: scope.FullPath,
            projectFingerprint: "fingerprint",
            projectRoot: scope.FullPath,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsProbable_WhenInputsManifestDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "probable-missing-manifest");
        PrepareRequiredInputs(scope);
        var evaluator = new IndexFreshnessEvaluator(new FileIndexCatalogReader(), new IndexInputSnapshotHasher());

        var result = await evaluator.Evaluate(
            storageRoot: scope.FullPath,
            projectFingerprint: "fingerprint",
            projectRoot: scope.FullPath,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsReadIndexFreshRequired_WhenRequireFreshAndFreshnessIsStale ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "require-fresh-stale");
        PrepareRequiredInputs(scope);
        var hasher = new IndexInputSnapshotHasher();
        var snapshot = await hasher.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        WriteInputsManifest(scope.FullPath, "fingerprint", snapshot!);
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"updated\": true }");
        var evaluator = new IndexFreshnessEvaluator(new FileIndexCatalogReader(), hasher);

        var result = await evaluator.Evaluate(
            storageRoot: scope.FullPath,
            projectFingerprint: "fingerprint",
            projectRoot: scope.FullPath,
            mode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsProbable_WhenModeIsDisabled ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "disabled");
        var evaluator = new IndexFreshnessEvaluator(new FileIndexCatalogReader(), new IndexInputSnapshotHasher());

        var result = await evaluator.Evaluate(
            storageRoot: scope.FullPath,
            projectFingerprint: "fingerprint",
            projectRoot: scope.FullPath,
            mode: ReadIndexMode.Disabled,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Null(result.Error);
    }

    private static void PrepareRequiredInputs (TestDirectoryScope scope)
    {
        scope.CreateDirectory(Path.Combine("Library", "ScriptAssemblies"));
        scope.CreateDirectory("Assets");
        scope.CreateDirectory("Packages");
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "initial");
        scope.WriteFile(Path.Combine("Packages", "manifest.json"), "{ \"dependencies\": {} }");
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"dependencies\": {} }");
    }

    private static void WriteInputsManifest (
        string storageRoot,
        string projectFingerprint,
        IndexInputHashSnapshot snapshot)
    {
        var manifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ScriptAssembliesHash: snapshot.ScriptAssembliesHash,
            PackagesManifestHash: snapshot.PackagesManifestHash,
            PackagesLockHash: snapshot.PackagesLockHash,
            AssemblyDefinitionHash: snapshot.AssemblyDefinitionHash,
            CombinedHash: snapshot.CombinedHash);
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        var directoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {manifestPath}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(manifestPath, IndexInputsManifestJsonContractSerializer.Serialize(manifest));
    }
}