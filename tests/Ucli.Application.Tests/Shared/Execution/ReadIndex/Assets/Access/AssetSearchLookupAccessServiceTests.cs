using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Assets;

public sealed class AssetSearchLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenAllowStaleIndexExists_ReturnsFilteredIndexEntries ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "asset-search-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Spawner.asset", "11111111111111111111111111111111", "Spawner", "Game.Spawner, Assembly-CSharp"),
                        CreateAssetSearchEntry("Assets/Data/Other.asset", "22222222222222222222222222222222", "Other", "Game.Other, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new StubAssetLookupSourceRefreshService();
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);
        var project = CreateProject();

        var result = await service.Search(
            project,
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            query: new AssetSearchLookupQuery(
                TypeId: "UnityEngine.Object, UnityEngine.CoreModule",
                PathPrefix: "Assets/Data",
                NameContains: "spawn"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.Entries);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output.Entries[0].AssetPath);
        Assert.Equal(AssetLookupSource.Index, result.Output.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.Equal(0, refreshService.CallCount);
        Assert.Equal(1, freshnessEvaluator.ObserveCallCount);
        Assert.Same(project, freshnessEvaluator.LastUnityProject);
        Assert.Equal(IndexFreshnessTarget.AssetSearchLookup, freshnessEvaluator.LastTarget);
        Assert.Equal("asset-search-hash", freshnessEvaluator.LastPersistedSourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "stale-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Stale.asset", "11111111111111111111111111111111", "Stale", "Game.Stale, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Stale),
        };
        var refreshService = new StubAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"),
                    AssetSearchEntries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Fresh.asset", "22222222222222222222222222222222", "Fresh", "Game.Fresh, Assembly-CSharp"),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing asset-search index freshness is 'stale'."),
        };
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            query: new AssetSearchLookupQuery(TypeId: null, PathPrefix: "Assets/Data", NameContains: "Fresh"),
            failFast: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.Entries);
        Assert.Equal("Assets/Data/Fresh.asset", result.Output.Entries[0].AssetPath);
        Assert.Equal(AssetLookupSource.Source, result.Output.AccessInfo.Source);
        Assert.Equal(UcliCommandIds.Query, refreshService.LastCommand);
        Assert.True(refreshService.LastFailFast);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenReadPostconditionRequiresNewerIndex_FallsBackToSourceEvenWhenAllowStale ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            AssetSearchLookupResult = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Success(
                new IndexAssetSearchLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    SourceInputsHash: "asset-search-hash",
                    Entries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Stale.asset", "11111111111111111111111111111111", "Stale", "Game.Stale, Assembly-CSharp"),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                ReadPostconditionTestFactory.Create(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00")),
                ])),
        };
        var refreshService = new StubAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:10+00:00"),
                    AssetSearchEntries:
                    [
                        CreateAssetSearchEntry("Assets/Data/Fresh.asset", "22222222222222222222222222222222", "Fresh", "Game.Fresh, Assembly-CSharp"),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing asset-search index generatedAtUtc is older than mutation read postcondition."),
        };
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService);

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            query: new AssetSearchLookupQuery(TypeId: null, PathPrefix: "Assets/Data", NameContains: "Fresh"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetLookupSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, readPostconditionStore.ReadCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenQueryIsEmpty_ReturnsInvalidArgument ()
    {
        var service = new AssetSearchLookupAccessService(
            new StubReadIndexArtifactReader(),
            new StubIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new StubAssetLookupSourceRefreshService());

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            query: new AssetSearchLookupQuery(null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
    }

    private static ResolvedUnityProjectContext CreateProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IndexAssetSearchEntryJsonContract CreateAssetSearchEntry (
        string assetPath,
        string assetGuid,
        string name,
        string typeId)
    {
        return new IndexAssetSearchEntryJsonContract(
            AssetPath: assetPath,
            AssetGuid: assetGuid,
            Name: name,
            TypeId: typeId,
            SearchTypeIds:
            [
                typeId,
                "UnityEngine.Object, UnityEngine.CoreModule",
            ]);
    }

    private sealed class StubReadIndexArtifactReader : IReadIndexArtifactReader
    {
        public ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract> AssetSearchLookupResult { get; set; }
            = ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>.Failure(IpcErrorCodes.ReadIndexBootstrapFailed, "missing");

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (ResolvedUnityProjectContext unityProject, string scenePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AssetSearchLookupResult);
        }
    }

    private sealed class StubIndexFreshnessEvaluator : IReadIndexFreshnessEvaluator
    {
        public IndexFreshnessEvaluationResult Result { get; set; }
            = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh);

        public int ObserveCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public IndexFreshnessTarget LastTarget { get; private set; }

        public string? LastPersistedSourceInputsHash { get; private set; }

        public ValueTask<IndexFreshnessEvaluationResult> Observe (
            ResolvedUnityProjectContext unityProject,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObserveCallCount++;
            LastUnityProject = unityProject;
            LastTarget = target;
            LastPersistedSourceInputsHash = persistedSourceInputsHash;
            return ValueTask.FromResult(Result);
        }

        public ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLite (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubAssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
    {
        public int CallCount { get; private set; }

        public UcliCommand LastCommand { get; private set; }

        public bool LastFailFast { get; private set; }

        public AssetLookupRefreshResult Result { get; set; }
            = AssetLookupRefreshResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<AssetLookupRefreshResult> Refresh (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            string fallbackReason,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastCommand = command;
            LastFailFast = failFast;
            return ValueTask.FromResult(Result);
        }
    }
}
