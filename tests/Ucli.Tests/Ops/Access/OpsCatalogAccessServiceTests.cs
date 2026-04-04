using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.Ops.Access;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexExists_ReturnsIndexWithoutEvaluatingFallbackOptions ()
    {
        var context = CreateContext();
        var indexReader = new StubIndexCatalogReader
        {
            OpsCatalogResult = IndexAccessResult<IndexOpsCatalogJsonContract>.Success(
                new IndexOpsCatalogJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    SourceInputsHash: "source-hash",
                    Entries:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var catalogReader = new StubOpsCatalogReader();
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            indexReader,
            freshnessEvaluator,
            new StubIndexInputFingerprintCalculator(),
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(context, ReadIndexMode.AllowStale),
            new OpsCommandInput(
                ProjectPath: null,
                Mode: "unsupported",
                Timeout: "abc",
                ReadIndexMode: ReadIndexModeValues.AllowStale));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output.Operations);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output.Operations[0].Name);
        Assert.Equal(OpsCatalogSource.Index, result.Output.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.True(result.Output.AccessInfo.Hit);
        Assert.Equal(IndexFreshness.Probable, result.Output.AccessInfo.Freshness);
        Assert.Equal(0, catalogReader.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var context = CreateContext();
        var indexReader = new StubIndexCatalogReader
        {
            OpsCatalogResult = IndexAccessResult<IndexOpsCatalogJsonContract>.Success(
                new IndexOpsCatalogJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    SourceInputsHash: "stale-hash",
                    Entries:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Stale),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var catalogReader = new StubOpsCatalogReader
        {
            Result = OpsCatalogFetchResult.Success(
                new IpcOpsReadResponse(
                    GeneratedAtUtc: generatedAtUtc,
                    Operations:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                            Kind: "mutation",
                            Policy: "advanced",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var inputFingerprintCalculator = new StubIndexInputFingerprintCalculator
        {
            Snapshot = new IndexInputHashSnapshot("script", "manifest", "lock", "asmdef", "combined"),
        };
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            indexReader,
            freshnessEvaluator,
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(context, ReadIndexMode.RequireFresh),
            new OpsCommandInput(
                ProjectPath: null,
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.RequireFresh));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output.Operations);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, result.Output.Operations[0].Name);
        Assert.Equal(OpsCatalogSource.Source, result.Output.AccessInfo.Source);
        Assert.False(result.Output.AccessInfo.Used);
        Assert.True(result.Output.AccessInfo.Hit);
        Assert.Equal(IndexFreshness.Fresh, result.Output.AccessInfo.Freshness);
        Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
        Assert.Contains("Existing ops index freshness is 'stale'.", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, catalogReader.CallCount);
        Assert.Null(catalogReader.LastMode);
        Assert.Null(catalogReader.LastTimeout);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(context.UnityProject.RepositoryRoot, store.StorageRoot);
        Assert.Equal(context.UnityProject.ProjectFingerprint, store.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSourcePersistenceFails_ReturnsSourceResultWithFallbackReason ()
    {
        var context = CreateContext();
        var catalogReader = new StubOpsCatalogReader
        {
            Result = OpsCatalogFetchResult.Success(
                new IpcOpsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                    Operations:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}"""),
                    ])),
        };
        var inputFingerprintCalculator = new StubIndexInputFingerprintCalculator
        {
            Snapshot = new IndexInputHashSnapshot("script", "manifest", "lock", "asmdef", "combined"),
        };
        var store = new StubOpsCatalogStore
        {
            WriteException = new InvalidOperationException("disk full"),
        };
        var service = CreateService(
            new StubIndexCatalogReader(),
            new StubIndexFreshnessEvaluator(),
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(context, ReadIndexMode.Disabled),
            new OpsCommandInput(
                ProjectPath: null,
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.Disabled));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output.Operations[0].Name);
        Assert.Equal(OpsCatalogSource.Source, result.Output.AccessInfo.Source);
        Assert.Contains("readIndex disabled by mode.", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Contains("Failed to persist refreshed ops readIndex. disk full", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    private static ProjectContext CreateContext ()
    {
        return new ProjectContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static OpsCatalogAccessService CreateService (
        IIndexCatalogReader indexCatalogReader,
        IIndexFreshnessEvaluator indexFreshnessEvaluator,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IOpsCatalogReader opsCatalogReader,
        IOpsCatalogStore opsCatalogStore)
    {
        return new OpsCatalogAccessService(
            indexCatalogReader,
            indexFreshnessEvaluator,
            indexInputFingerprintCalculator,
            opsCatalogReader,
            opsCatalogStore);
    }

    private sealed class StubIndexCatalogReader : IIndexCatalogReader
    {
        public IndexAccessResult<IndexOpsCatalogJsonContract> OpsCatalogResult { get; set; }
            = IndexAccessResult<IndexOpsCatalogJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: ops.catalog.json.");

        public ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(OpsCatalogResult);
        }

        public ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubIndexFreshnessEvaluator : IIndexFreshnessEvaluator
    {
        public IndexFreshnessEvaluationResult Result { get; set; }
            = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh);

        public ValueTask<IndexFreshnessEvaluationResult> Evaluate (
            string storageRoot,
            string projectFingerprint,
            string projectRoot,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
    {
        public IndexInputHashSnapshot? Snapshot { get; set; }

        public ValueTask<IndexInputHashSnapshot?> TryCompute (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Snapshot);
        }
    }

    private sealed class StubOpsCatalogReader : IOpsCatalogReader
    {
        public int CallCount { get; private set; }

        public string? LastMode { get; private set; }

        public string? LastTimeout { get; private set; }

        public OpsCatalogFetchResult Result { get; set; }
            = OpsCatalogFetchResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<OpsCatalogFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            string? mode,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMode = mode;
            LastTimeout = timeout;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsCatalogStore : IOpsCatalogStore
    {
        public int CallCount { get; private set; }

        public string? StorageRoot { get; private set; }

        public string? ProjectFingerprint { get; private set; }

        public Exception? WriteException { get; set; }

        public ValueTask Write (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            IndexInputHashSnapshot inputSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            StorageRoot = storageRoot;
            ProjectFingerprint = projectFingerprint;

            if (WriteException != null)
            {
                throw WriteException;
            }

            return ValueTask.CompletedTask;
        }
    }
}