using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Assets;

public sealed class GuidPathLookupAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetGuid_WhenAllowStaleIndexExists_ReturnsIndexEntry ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
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
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);
        var project = CreateProject();

        var result = await service.TryResolveAssetGuid(
            project,
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.AllowStale,
            assetGuid: "11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal("Assets/Data/Spawner.asset", result.Output!.Entry!.AssetPath);
        Assert.Equal(AssetLookupSource.Index, result.Output.AccessInfo.Source);
        Assert.Equal(0, refreshService.CallCount);
        Assert.Equal(1, freshnessEvaluator.ObserveCallCount);
        Assert.Same(project, freshnessEvaluator.LastUnityProject);
        Assert.Equal(IndexFreshnessTarget.GuidPathLookup, freshnessEvaluator.LastTarget);
        Assert.Equal("guid-path-hash", freshnessEvaluator.LastPersistedSourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
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
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService);

        var result = await service.TryResolveAssetPath(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
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
    public async Task TryResolveAssetGuid_WhenReadPostconditionStoreFails_FallsBackToSource ()
    {
        var indexReader = new StubReadIndexArtifactReader
        {
            GuidPathLookupResult = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Success(
                new IndexGuidPathLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    SourceInputsHash: "guid-path-hash",
                    Entries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ])),
        };
        var freshnessEvaluator = new StubIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Failure(
                ExecutionError.InvalidArgument("Mutation read postcondition is invalid: /repo/.ucli/local/fingerprints/project-fingerprint/mutation-read-postcondition.json.")),
        };
        var refreshService = new StubAssetLookupSourceRefreshService
        {
            Result = AssetLookupRefreshResult.Success(
                new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:01:00+00:00"),
                    AssetSearchEntries: [],
                    GuidPathEntries:
                    [
                        new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Data/Spawner.asset"),
                    ]),
                "Mutation read postcondition is invalid."),
        };
        var service = new GuidPathLookupAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService);

        var result = await service.TryResolveAssetGuid(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
            assetGuid: "11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetLookupSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("Mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, readPostconditionStore.ReadCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryResolveAssetPath_WhenPathIsOutsideAssets_ReturnsInvalidArgument ()
    {
        var service = new GuidPathLookupAccessService(
            new StubReadIndexArtifactReader(),
            new StubIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new StubAssetLookupSourceRefreshService());

        var result = await service.TryResolveAssetPath(
            CreateProject(),
            UcliConfig.CreateDefault(),
            mode: UnityExecutionMode.Auto,
            timeout: TimeSpan.FromMilliseconds(1200),
            readIndexMode: ReadIndexMode.RequireFresh,
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

    private sealed class StubReadIndexArtifactReader : IReadIndexArtifactReader
    {
        public ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract> GuidPathLookupResult { get; set; }
            = ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>.Failure(IpcErrorCodes.ReadIndexBootstrapFailed, "missing");

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (ResolvedUnityProjectContext unityProject, string scenePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (ResolvedUnityProjectContext unityProject, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GuidPathLookupResult);
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
            return ValueTask.FromResult(Result);
        }
    }
}
