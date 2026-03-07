using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ops;

public sealed class OpsServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenAllowStaleIndexExists_ReturnsIndexWithoutEvaluatingLiveOptions ()
    {
        var context = CreateContext();
        var initResolver = new StubInitStatusContextResolver(context);
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
                            Name: "ucli.go.describe",
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var modeDecisionService = new StubUnityExecutionModeDecisionService();
        var liveReader = new StubOpsCatalogLiveReader();
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            initResolver,
            indexReader,
            freshnessEvaluator,
            new StubIndexInputFingerprintCalculator(),
            modeDecisionService,
            liveReader,
            store);

        var result = await service.List(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: "unsupported",
                Timeout: "abc",
                ReadIndexMode: ReadIndexModeValues.AllowStale));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output.Operations);
        Assert.Equal("ucli.go.describe", result.Output.Operations[0].Name);
        Assert.Equal("index", result.Output.ReadIndex.Source);
        Assert.True(result.Output.ReadIndex.Used);
        Assert.True(result.Output.ReadIndex.Hit);
        Assert.Equal("probable", result.Output.ReadIndex.Freshness);
        Assert.Equal(0, modeDecisionService.CallCount);
        Assert.Equal(0, liveReader.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenRequireFreshIndexIsStale_FallsBackToLiveUnity ()
    {
        var context = CreateContext();
        var initResolver = new StubInitStatusContextResolver(context);
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
                            Name: "ucli.go.describe",
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Stale),
        };
        var liveGeneratedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var modeDecisionService = new StubUnityExecutionModeDecisionService
        {
            Result = UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    RequestedMode: UnityExecutionMode.Auto,
                    DaemonRunning: true,
                    Target: UnityExecutionTarget.Daemon)),
        };
        var liveReader = new StubOpsCatalogLiveReader
        {
            Result = OpsCatalogLiveReadResult.Success(
                new IpcOpsReadResponse(
                    GeneratedAtUtc: liveGeneratedAtUtc,
                    Operations:
                    [
                        new IndexOpEntryJsonContract(
                            Name: "ucli.scene.save",
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
            initResolver,
            indexReader,
            freshnessEvaluator,
            inputFingerprintCalculator,
            modeDecisionService,
            liveReader,
            store);

        var result = await service.List(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.RequireFresh));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output.Operations);
        Assert.Equal("ucli.scene.save", result.Output.Operations[0].Name);
        Assert.Equal("unity", result.Output.ReadIndex.Source);
        Assert.False(result.Output.ReadIndex.Used);
        Assert.True(result.Output.ReadIndex.Hit);
        Assert.Equal("fresh", result.Output.ReadIndex.Freshness);
        Assert.Equal(liveGeneratedAtUtc, result.Output.ReadIndex.GeneratedAtUtc);
        Assert.Contains("Existing ops index freshness is 'stale'.", result.Output.ReadIndex.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, modeDecisionService.CallCount);
        Assert.Equal(1, liveReader.CallCount);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(context.UnityProject.RepositoryRoot, store.StorageRoot);
        Assert.Equal(context.UnityProject.ProjectFingerprint, store.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Describe_WhenLivePersistenceFails_ReturnsUnityResultWithFallbackReason ()
    {
        var context = CreateContext();
        var initResolver = new StubInitStatusContextResolver(context);
        var modeDecisionService = new StubUnityExecutionModeDecisionService
        {
            Result = UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    RequestedMode: UnityExecutionMode.Auto,
                    DaemonRunning: false,
                    Target: UnityExecutionTarget.Oneshot)),
        };
        var liveReader = new StubOpsCatalogLiveReader
        {
            Result = OpsCatalogLiveReadResult.Success(
                new IpcOpsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                    Operations:
                    [
                        new IndexOpEntryJsonContract(
                            Name: "ucli.go.describe",
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
            initResolver,
            new StubIndexCatalogReader(),
            new StubIndexFreshnessEvaluator(),
            inputFingerprintCalculator,
            modeDecisionService,
            liveReader,
            store);

        var result = await service.Describe(
            new OpsDescribeCommandInput(
                OperationName: "ucli.go.describe",
                ProjectPath: null,
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.Disabled));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("ucli.go.describe", result.Output.Operation.Name);
        Assert.Equal("unity", result.Output.ReadIndex.Source);
        Assert.Contains("readIndex disabled by mode.", result.Output.ReadIndex.FallbackReason, StringComparison.Ordinal);
        Assert.Contains("Failed to persist refreshed ops readIndex. disk full", result.Output.ReadIndex.FallbackReason, StringComparison.Ordinal);
        Assert.Equal("object", result.Output.Operation.ArgsSchema.GetProperty("type").GetString());
    }

    private static InitStatusContext CreateContext ()
    {
        return new InitStatusContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static OpsService CreateService (
        IInitStatusContextResolver initStatusContextResolver,
        IIndexCatalogReader indexCatalogReader,
        IIndexFreshnessEvaluator indexFreshnessEvaluator,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IUnityExecutionModeDecisionService modeDecisionService,
        IOpsCatalogLiveReader opsCatalogLiveReader,
        IOpsCatalogStore opsCatalogStore)
    {
        return new OpsService(
            initStatusContextResolver,
            indexCatalogReader,
            indexFreshnessEvaluator,
            indexInputFingerprintCalculator,
            modeDecisionService,
            opsCatalogLiveReader,
            opsCatalogStore);
    }

    private sealed class StubInitStatusContextResolver : IInitStatusContextResolver
    {
        private readonly InitStatusContext context;

        public StubInitStatusContextResolver (InitStatusContext context)
        {
            this.context = context;
        }

        public ValueTask<InitStatusContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(InitStatusContextResolutionResult.Success(context));
        }
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

    private sealed class StubUnityExecutionModeDecisionService : IUnityExecutionModeDecisionService
    {
        public int CallCount { get; private set; }

        public UnityExecutionModeDecisionResult Result { get; set; }
            = UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    RequestedMode: UnityExecutionMode.Auto,
                    DaemonRunning: true,
                    Target: UnityExecutionTarget.Daemon));

        public ValueTask<UnityExecutionModeDecisionResult> Decide (
            UcliCommand command,
            string? mode,
            string? timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsCatalogLiveReader : IOpsCatalogLiveReader
    {
        public int CallCount { get; private set; }

        public OpsCatalogLiveReadResult Result { get; set; }
            = OpsCatalogLiveReadResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<OpsCatalogLiveReadResult> Read (
            ResolvedUnityProjectContext unityProject,
            UnityExecutionTarget target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
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