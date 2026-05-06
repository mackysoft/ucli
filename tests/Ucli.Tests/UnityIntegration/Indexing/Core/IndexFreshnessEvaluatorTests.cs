using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsFresh_WhenPersistedHashMatchesCurrentCoreSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "fresh");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var snapshot = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: snapshot!.CombinedHash,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsStale_WhenPersistedHashDiffersFromCurrentCoreSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "stale");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var snapshot = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "updated");
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: snapshot!.CombinedHash,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsProbable_WhenPersistedHashIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "probable-missing-hash");
        PrepareRequiredInputs(scope);
        var evaluator = new IndexFreshnessEvaluator(new FileSystemIndexInputFingerprintCalculator());

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: null,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsReadIndexFreshRequired_WhenRequireFreshAndPersistedHashIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "require-fresh-missing-hash");
        PrepareRequiredInputs(scope);
        var evaluator = new IndexFreshnessEvaluator(new FileSystemIndexInputFingerprintCalculator());

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: null,
            mode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsReadIndexFreshRequired_WhenRequireFreshAndFreshnessIsStale ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "require-fresh-stale");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var snapshot = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"updated\": true }");
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: snapshot!.CombinedHash,
            mode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsFresh_ForGuidPathLookup_WhenOnlyCodeInputsChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "guid-path-code-change");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var snapshot = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "updated");
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.GuidPathLookup,
            persistedSourceInputsHash: snapshot!.GuidPathHash,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsStale_ForAssetSearchLookup_WhenAssetInputsChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "asset-search-asset-change");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var snapshot = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(snapshot);
        scope.WriteFile(Path.Combine("Assets", "Sample.asset"), "updated");
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.AssetSearchLookup,
            persistedSourceInputsHash: snapshot!.AssetSearchHash,
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsProbable_WhenModeIsDisabled ()
    {
        using var scope = TestDirectories.CreateTempScope("index-freshness", "disabled");
        var evaluator = new IndexFreshnessEvaluator(new FileSystemIndexInputFingerprintCalculator());

        var result = await evaluator.Evaluate(
            projectRoot: scope.FullPath,
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: "hash",
            mode: ReadIndexMode.Disabled,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_UsesCoreSnapshot_ForOpsCatalogTarget ()
    {
        var calculator = new StubIndexInputFingerprintCalculator
        {
            CoreSnapshot = new IndexCoreInputHashSnapshot(
                ScriptAssembliesHash: "script-hash",
                PackagesManifestHash: "manifest-hash",
                PackagesLockHash: "lock-hash",
                AssemblyDefinitionHash: "asm-hash",
                CombinedHash: "combined-hash"),
            ThrowOnTryCompute = true,
        };
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: "/repo/project",
            target: IndexFreshnessTarget.OpsCatalog,
            persistedSourceInputsHash: "combined-hash",
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Equal(1, calculator.CoreCallCount);
        Assert.Equal(0, calculator.FullCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_UsesFullSnapshot_ForAssetSearchLookupTarget ()
    {
        var calculator = new StubIndexInputFingerprintCalculator
        {
            Snapshot = new IndexInputHashSnapshot(
                ScriptAssembliesHash: "script-hash",
                PackagesManifestHash: "manifest-hash",
                PackagesLockHash: "lock-hash",
                AssemblyDefinitionHash: "asm-hash",
                AssetsContentHash: "assets-hash",
                AssetSearchHash: "asset-search-hash",
                GuidPathHash: "guid-path-hash",
                CombinedHash: "combined-hash"),
        };
        var evaluator = new IndexFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRoot: "/repo/project",
            target: IndexFreshnessTarget.AssetSearchLookup,
            persistedSourceInputsHash: "asset-search-hash",
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
        Assert.Equal(0, calculator.CoreCallCount);
        Assert.Equal(1, calculator.FullCallCount);
    }

    private static void PrepareRequiredInputs (TestDirectoryScope scope)
    {
        scope.CreateDirectory(Path.Combine("Library", "ScriptAssemblies"));
        scope.CreateDirectory("Assets");
        scope.CreateDirectory("Packages");
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "initial");
        scope.WriteFile(Path.Combine("Assets", "Sample.asset"), "initial");
        scope.WriteFile(Path.Combine("Assets", "Sample.asset.meta"), "guid: sample");
        scope.WriteFile(Path.Combine("Packages", "manifest.json"), "{ \"dependencies\": {} }");
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"dependencies\": {} }");
    }

    private sealed class StubIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
    {
        public int CoreCallCount { get; private set; }

        public int FullCallCount { get; private set; }

        public IndexCoreInputHashSnapshot? CoreSnapshot { get; set; }

        public IndexInputHashSnapshot? Snapshot { get; set; }

        public bool ThrowOnTryCompute { get; set; }

        public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCore (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CoreCallCount++;
            return ValueTask.FromResult(CoreSnapshot);
        }

        public ValueTask<IndexInputHashSnapshot?> TryCompute (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FullCallCount++;
            if (ThrowOnTryCompute)
            {
                throw new InvalidOperationException("full snapshot should not be computed");
            }

            return ValueTask.FromResult(Snapshot);
        }
    }
}
