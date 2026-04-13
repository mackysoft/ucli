using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.Ops.Access;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexExists_ReturnsIndexWithoutEvaluatingFallbackOptions ()
    {
        var context = CreateContext();
        var snapshotLoader = new StubPersistedOpsCatalogSnapshotLoader
        {
            Result = PersistedOpsCatalogSnapshotLoadResult.Success(
                new PersistedOpsCatalogSnapshot(
                    Entries:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ],
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    Freshness: IndexFreshness.Probable)),
        };
        var catalogReader = new StubOpsCatalogReader();
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            snapshotLoader,
            new StubIndexCatalogReader(),
            new StubIndexInputFingerprintCalculator(),
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(
                context,
                ReadIndexMode.AllowStale,
                UnityExecutionMode.Auto,
                TimeSpan.FromMilliseconds(1200),
                true));

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
        var snapshotLoader = new StubPersistedOpsCatalogSnapshotLoader
        {
            Result = PersistedOpsCatalogSnapshotLoadResult.Success(
                new PersistedOpsCatalogSnapshot(
                    Entries:
                    [
                        new IndexOpEntryJsonContract(
                            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ],
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    Freshness: IndexFreshness.Stale)),
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
            Snapshot = new IndexInputHashSnapshot("script", "manifest", "lock", "asmdef", "assets", "asset-search", "guid-path", "combined"),
        };
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            snapshotLoader,
            new StubIndexCatalogReader(),
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(
                context,
                ReadIndexMode.RequireFresh,
                UnityExecutionMode.Auto,
                TimeSpan.FromMilliseconds(1200),
                true));

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
        Assert.Equal(UnityExecutionMode.Auto, catalogReader.LastMode);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), catalogReader.LastTimeout);
        Assert.True(catalogReader.LastFailFast);
        Assert.True(catalogReader.LastRequireReadinessGate);
        Assert.Equal(1, inputFingerprintCalculator.FullCallCount);
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
            Snapshot = new IndexInputHashSnapshot("script", "manifest", "lock", "asmdef", "assets", "asset-search", "guid-path", "combined"),
        };
        var store = new StubOpsCatalogStore
        {
            WriteException = new InvalidOperationException("disk full"),
        };
        var service = CreateService(
            new StubPersistedOpsCatalogSnapshotLoader(),
            new StubIndexCatalogReader(),
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(
                context,
                ReadIndexMode.Disabled,
                UnityExecutionMode.Auto,
                TimeSpan.FromMilliseconds(1200),
                false));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output.Operations[0].Name);
        Assert.Equal(OpsCatalogSource.Source, result.Output.AccessInfo.Source);
        Assert.Contains("readIndex disabled by mode.", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Contains("Failed to persist refreshed ops readIndex. disk full", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenManifestIsMissingButLookupExists_PersistsOpsCatalogWithoutRegeneratingLookupHashes ()
    {
        var context = CreateContext();
        var indexReader = new StubIndexCatalogReader
        {
            AssetSearchLookupResult = IndexAccessResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    SourceInputsHash: "stale-asset-search-hash",
                    Entries:
                    [
                        new IndexAssetSearchEntryJsonContract(
                            AssetPath: "Assets/Data/Stale.asset",
                            AssetGuid: "11111111111111111111111111111111",
                            Name: "Stale",
                            TypeId: "Game.Stale, Assembly-CSharp",
                            SearchTypeIds:
                            [
                                "Game.Stale, Assembly-CSharp",
                                "UnityEngine.Object, UnityEngine.CoreModule",
                            ]),
                    ])),
        };
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
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var inputFingerprintCalculator = new StubIndexInputFingerprintCalculator
        {
            CoreSnapshot = new IndexCoreInputHashSnapshot(
                ScriptAssembliesHash: "script",
                PackagesManifestHash: "manifest",
                PackagesLockHash: "lock",
                AssemblyDefinitionHash: "asmdef",
                CombinedHash: "combined"),
            ThrowOnTryCompute = true,
        };
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            new StubPersistedOpsCatalogSnapshotLoader(),
            indexReader,
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(
                context,
                ReadIndexMode.Disabled,
                UnityExecutionMode.Auto,
                TimeSpan.FromMilliseconds(1200),
                false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, inputFingerprintCalculator.CoreCallCount);
        Assert.Equal(0, inputFingerprintCalculator.FullCallCount);
        Assert.Equal(1, store.CallCount);
        Assert.Equal("combined", store.LastSourceInputsHash);
        Assert.Null(store.LastManifestInputSnapshot);
        Assert.Equal("readIndex disabled by mode.", result.Output!.AccessInfo.FallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPersistingSourceResult_ReusesManifestAssetHashesWithoutFullFingerprint ()
    {
        var context = CreateContext();
        var indexReader = new StubIndexCatalogReader
        {
            ManifestResult = IndexAccessResult<IndexInputsManifestJsonContract>.Success(
                new IndexInputsManifestJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                    ScriptAssembliesHash: "old-script",
                    PackagesManifestHash: "old-manifest",
                    PackagesLockHash: "old-lock",
                    AssemblyDefinitionHash: "old-asm",
                    AssetsContentHash: "existing-assets",
                    AssetSearchHash: "existing-asset-search",
                    GuidPathHash: "existing-guid-path",
                    CombinedHash: "old-combined")),
        };
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
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ])),
        };
        var inputFingerprintCalculator = new StubIndexInputFingerprintCalculator
        {
            CoreSnapshot = new IndexCoreInputHashSnapshot(
                ScriptAssembliesHash: "script",
                PackagesManifestHash: "manifest",
                PackagesLockHash: "lock",
                AssemblyDefinitionHash: "asmdef",
                CombinedHash: "combined"),
            ThrowOnTryCompute = true,
        };
        var store = new StubOpsCatalogStore();
        var service = CreateService(
            new StubPersistedOpsCatalogSnapshotLoader(),
            indexReader,
            inputFingerprintCalculator,
            catalogReader,
            store);

        var result = await service.Read(
            new OpsPreflightContext(
                context,
                ReadIndexMode.Disabled,
                UnityExecutionMode.Auto,
                TimeSpan.FromMilliseconds(1200),
                false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, inputFingerprintCalculator.CoreCallCount);
        Assert.Equal(0, inputFingerprintCalculator.FullCallCount);
        Assert.Equal(1, indexReader.ReadInputsManifestCallCount);
        Assert.Equal("combined", store.LastSourceInputsHash);
        Assert.NotNull(store.LastManifestInputSnapshot);
        Assert.Equal("script", store.LastManifestInputSnapshot!.ScriptAssembliesHash);
        Assert.Equal("manifest", store.LastManifestInputSnapshot.PackagesManifestHash);
        Assert.Equal("lock", store.LastManifestInputSnapshot.PackagesLockHash);
        Assert.Equal("asmdef", store.LastManifestInputSnapshot.AssemblyDefinitionHash);
        Assert.Equal("existing-assets", store.LastManifestInputSnapshot.AssetsContentHash);
        Assert.Equal("existing-asset-search", store.LastManifestInputSnapshot.AssetSearchHash);
        Assert.Equal("existing-guid-path", store.LastManifestInputSnapshot.GuidPathHash);
        Assert.Equal("combined", store.LastManifestInputSnapshot.CombinedHash);
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
        IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader,
        IIndexCatalogReader indexCatalogReader,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IOpsCatalogReader opsCatalogReader,
        IOpsCatalogStore opsCatalogStore)
    {
        return new OpsCatalogAccessService(
            persistedOpsCatalogSnapshotLoader,
            indexCatalogReader,
            indexInputFingerprintCalculator,
            opsCatalogReader,
            opsCatalogStore);
    }

    private sealed class StubPersistedOpsCatalogSnapshotLoader : IPersistedOpsCatalogSnapshotLoader
    {
        public PersistedOpsCatalogSnapshotLoadResult Result { get; set; }
            = PersistedOpsCatalogSnapshotLoadResult.Failure(
                new IndexServiceError(
                    IpcErrorCodes.ReadIndexBootstrapFailed,
                    "Index contract file was not found: ops.catalog.json."));

        public ValueTask<PersistedOpsCatalogSnapshotLoadResult> Load (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubIndexCatalogReader : IIndexCatalogReader
    {
        public IndexAccessResult<IndexOpsCatalogJsonContract> OpsCatalogResult { get; set; }
            = IndexAccessResult<IndexOpsCatalogJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: ops.catalog.json.");

        public IndexAccessResult<IndexAssetSearchLookupJsonContract> AssetSearchLookupResult { get; set; }
            = IndexAccessResult<IndexAssetSearchLookupJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: lookups/asset-search.lookup.json.");

        public IndexAccessResult<IndexGuidPathLookupJsonContract> GuidPathLookupResult { get; set; }
            = IndexAccessResult<IndexGuidPathLookupJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: lookups/guid-path.lookup.json.");

        public int ReadInputsManifestCallCount { get; private set; }

        public IndexAccessResult<IndexInputsManifestJsonContract> ManifestResult { get; set; }
            = IndexAccessResult<IndexInputsManifestJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: inputs/manifest.json.");

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

        public ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AssetSearchLookupResult);
        }

        public ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GuidPathLookupResult);
        }

        public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadInputsManifestCallCount++;
            return ValueTask.FromResult(ManifestResult);
        }
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
            if (CoreSnapshot != null)
            {
                return ValueTask.FromResult<IndexCoreInputHashSnapshot?>(CoreSnapshot);
            }

            if (Snapshot != null)
            {
                return ValueTask.FromResult<IndexCoreInputHashSnapshot?>(
                    new IndexCoreInputHashSnapshot(
                        ScriptAssembliesHash: Snapshot.ScriptAssembliesHash,
                        PackagesManifestHash: Snapshot.PackagesManifestHash,
                        PackagesLockHash: Snapshot.PackagesLockHash,
                        AssemblyDefinitionHash: Snapshot.AssemblyDefinitionHash,
                        CombinedHash: Snapshot.CombinedHash));
            }

            return ValueTask.FromResult<IndexCoreInputHashSnapshot?>(null);
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

    private sealed class StubOpsCatalogReader : IOpsCatalogReader
    {
        public int CallCount { get; private set; }

        public UnityExecutionMode LastMode { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public bool LastFailFast { get; private set; }

        public bool LastRequireReadinessGate { get; private set; }

        public OpsCatalogFetchResult Result { get; set; }
            = OpsCatalogFetchResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<OpsCatalogFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast,
            bool requireReadinessGate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMode = mode;
            LastTimeout = timeout;
            LastFailFast = failFast;
            LastRequireReadinessGate = requireReadinessGate;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsCatalogStore : IOpsCatalogStore
    {
        public int CallCount { get; private set; }

        public string? StorageRoot { get; private set; }

        public string? ProjectFingerprint { get; private set; }

        public string? LastSourceInputsHash { get; private set; }

        public IndexInputHashSnapshot? LastManifestInputSnapshot { get; private set; }

        public Exception? WriteException { get; set; }

        public ValueTask Write (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string sourceInputsHash,
            IndexInputHashSnapshot? manifestInputSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            StorageRoot = storageRoot;
            ProjectFingerprint = projectFingerprint;
            LastSourceInputsHash = sourceInputsHash;
            LastManifestInputSnapshot = manifestInputSnapshot;

            if (WriteException != null)
            {
                throw WriteException;
            }

            return ValueTask.CompletedTask;
        }
    }
}