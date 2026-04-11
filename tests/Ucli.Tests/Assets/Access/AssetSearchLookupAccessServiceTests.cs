using MackySoft.Ucli.Assets;
using MackySoft.Ucli.Assets.Access;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Assets.Access;

public sealed class AssetSearchLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenAllowStaleIndexExists_ReturnsFilteredIndexEntries ()
    {
        var indexReader = new StubIndexCatalogReader
        {
            AssetSearchLookupResult = IndexAccessResult<IndexAssetSearchLookupJsonContract>.Success(
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
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, refreshService);

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new StubIndexCatalogReader
        {
            AssetSearchLookupResult = IndexAccessResult<IndexAssetSearchLookupJsonContract>.Success(
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
        var service = new AssetSearchLookupAccessService(indexReader, freshnessEvaluator, refreshService);

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
            readIndexMode: ReadIndexMode.RequireFresh,
            query: new AssetSearchLookupQuery(TypeId: null, PathPrefix: "Assets/Data", NameContains: "Fresh"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.Entries);
        Assert.Equal("Assets/Data/Fresh.asset", result.Output.Entries[0].AssetPath);
        Assert.Equal(AssetLookupSource.Source, result.Output.AccessInfo.Source);
        Assert.Equal(UcliCommandIds.Query, refreshService.LastCommand);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Search_WhenQueryIsEmpty_ReturnsInvalidArgument ()
    {
        var service = new AssetSearchLookupAccessService(
            new StubIndexCatalogReader(),
            new StubIndexFreshnessEvaluator(),
            new StubAssetLookupSourceRefreshService());

        var result = await service.Search(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
            readIndexMode: ReadIndexMode.AllowStale,
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

    private sealed class StubIndexCatalogReader : IIndexCatalogReader
    {
        public IndexAccessResult<IndexAssetSearchLookupJsonContract> AssetSearchLookupResult { get; set; }
            = IndexAccessResult<IndexAssetSearchLookupJsonContract>.Failure(IpcErrorCodes.ReadIndexBootstrapFailed, "missing");

        public ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(AssetSearchLookupResult);
        }
    }

    private sealed class StubIndexFreshnessEvaluator : IIndexFreshnessEvaluator
    {
        public IndexFreshnessEvaluationResult Result { get; set; }
            = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh);

        public ValueTask<IndexFreshnessEvaluationResult> Evaluate (
            string projectRoot,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubAssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
    {
        public int CallCount { get; private set; }

        public UcliCommand LastCommand { get; private set; }

        public AssetLookupRefreshResult Result { get; set; }
            = AssetLookupRefreshResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<AssetLookupRefreshResult> Refresh (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            string? mode,
            string? timeout,
            ReadIndexMode readIndexMode,
            string fallbackReason,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastCommand = command;
            return ValueTask.FromResult(Result);
        }
    }
}