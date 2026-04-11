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

public sealed class GuidPathLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetGuid_WhenAllowStaleIndexExists_ReturnsIndexEntry ()
    {
        var indexReader = new StubIndexCatalogReader
        {
            GuidPathLookupResult = IndexAccessResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new StubAssetLookupSourceRefreshService();
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, refreshService);

        var result = await service.TryResolveAssetGuid(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
            readIndexMode: ReadIndexMode.AllowStale,
            assetGuid: "11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output!.Entry!.AssetPath);
        Assert.Equal(AssetLookupSource.Index, result.Output.AccessInfo.Source);
        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new StubIndexCatalogReader
        {
            GuidPathLookupResult = IndexAccessResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Stale.asset"),
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
                        new IndexAssetSearchEntryJsonContract(
                            AssetPath: "Assets/Data/Fresh.asset",
                            AssetGuid: "22222222222222222222222222222222",
                            Name: "Fresh",
                            TypeId: "Game.Fresh, Assembly-CSharp",
                            SearchTypeIds:
                            [
                                "Game.Fresh, Assembly-CSharp",
                                "UnityEngine.Object, UnityEngine.CoreModule",
                            ]),
                    ],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Data/Fresh.asset"),
                    ]),
                "Existing guid-path index freshness is 'stale'."),
        };
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, refreshService);

        var result = await service.TryResolveAssetPath(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
            readIndexMode: ReadIndexMode.RequireFresh,
            assetPath: "Assets/Data/Fresh.asset");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("22222222222222222222222222222222", result.Output!.Entry!.AssetGuid);
        Assert.Equal(AssetLookupSource.Source, result.Output.AccessInfo.Source);
        Assert.Equal(UcliCommandIds.Resolve, refreshService.LastCommand);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenPathIsOutsideAssets_ReturnsInvalidArgument ()
    {
        var service = new GuidPathLookupAccessService(
            new StubIndexCatalogReader(),
            new StubIndexFreshnessEvaluator(),
            new StubAssetLookupSourceRefreshService());

        var result = await service.TryResolveAssetPath(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: null,
            timeout: null,
            readIndexMode: ReadIndexMode.AllowStale,
            assetPath: "Packages/com.example/Test.asset");

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

    private sealed class StubIndexCatalogReader : IIndexCatalogReader
    {
        public IndexAccessResult<IndexGuidPathLookupJsonContract> GuidPathLookupResult { get; set; }
            = IndexAccessResult<IndexGuidPathLookupJsonContract>.Failure(IpcErrorCodes.ReadIndexBootstrapFailed, "missing");

        public ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GuidPathLookupResult);
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