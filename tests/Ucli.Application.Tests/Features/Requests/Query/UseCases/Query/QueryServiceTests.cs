using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class QueryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAssetsFindLookupSucceeds_ForwardsFailFastAndReturnsWindowedPlanResultWithoutUnityExecution ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var assetSearchLookupAccessService = new StubAssetSearchLookupAccessService(AssetSearchLookupReadResult.Success(
            new AssetSearchLookupReadOutput(
                Entries:
                [
                    new IndexAssetSearchEntryJsonContract(
                        AssetPath: "Assets/A.mat",
                        AssetGuid: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        Name: "A",
                        TypeId: "UnityEngine.Material, UnityEngine.CoreModule",
                        SearchTypeIds: ["UnityEngine.Material, UnityEngine.CoreModule"]),
                    new IndexAssetSearchEntryJsonContract(
                        AssetPath: "Assets/B.mat",
                        AssetGuid: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        Name: "B",
                        TypeId: "UnityEngine.Material, UnityEngine.CoreModule",
                        SearchTypeIds: ["UnityEngine.Material, UnityEngine.CoreModule"]),
                ],
                AccessInfo: new AssetLookupAccessInfo(
                    Used: true,
                    Hit: true,
                    Source: AssetLookupSource.Index,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                    FallbackReason: null)),
            "Asset-search lookup read completed."));
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService();
        var unityRequestExecutor = new SpyUnityRequestExecutor();
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                new QueryAssetsFindOperationRequest(
                    CommandName: "query.assets.find",
                    OperationId: "assets.find",
                    OperationName: UcliPrimitiveOperationNames.AssetsFind,
                    Filter: new QueryAssetsFindFilter("UnityEngine.Material, UnityEngine.CoreModule", null, null),
                    WindowOptions: new QueryWindowOptions(
                        All: false,
                        Limit: 1,
                        After: null,
                        Offset: 0)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Equal("query.assets.find", result.CommandName);
        Assert.Equal(1, assetSearchLookupAccessService.CallCount);
        Assert.True(assetSearchLookupAccessService.CapturedFailFast);
        Assert.NotNull(assetSearchLookupAccessService.CapturedQuery);
        Assert.Equal("UnityEngine.Material, UnityEngine.CoreModule", assetSearchLookupAccessService.CapturedQuery!.TypeId);
        Assert.Equal(0, sceneTreeLiteAccessService.CallCount);
        Assert.Equal(0, unityRequestExecutor.CallCount);
        Assert.True(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoSource.Index, result.ReadIndex.Source);

        var opResult = Assert.Single(result.OpResults);
        Assert.Equal("assets.find", opResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.AssetsFind, opResult.Op);
        Assert.True(opResult.Result.HasValue);
        var payload = opResult.Result!.Value;
        Assert.Equal(1, payload.GetProperty("matches").GetArrayLength());
        Assert.Equal(2, payload.GetProperty("window").GetProperty("totalCount").GetInt32());
        Assert.False(payload.GetProperty("window").GetProperty("isComplete").GetBoolean());
        Assert.True(payload.GetProperty("window").TryGetProperty("nextCursor", out var nextCursor));
        Assert.False(string.IsNullOrWhiteSpace(nextCursor.GetString()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneTreeLookupSucceeds_ForwardsFailFastAndReturnsWindowedRoots ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var assetSearchLookupAccessService = new StubAssetSearchLookupAccessService();
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService(SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                ScenePath: "Assets/Scenes/Main.unity",
                Roots:
                [
                    new IndexSceneTreeLiteNodeJsonContract("Root", "GlobalObjectId_V1-1-2-3-4-5-6", []),
                ],
                AccessInfo: new SceneTreeLiteAccessInfo(
                    Used: true,
                    Hit: true,
                    Source: SceneTreeLiteSource.Index,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                    FallbackReason: null)),
            "Scene-tree-lite read completed."));
        var unityRequestExecutor = new SpyUnityRequestExecutor();
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                new QuerySceneTreeOperationRequest(
                    CommandName: "query.scene.tree",
                    OperationId: "scene.tree",
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: "Assets/Scenes/Main.unity",
                    Depth: 1,
                    WindowOptions: new QueryWindowOptions(
                        All: false,
                        Limit: 100,
                        After: null,
                        Offset: 0)),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, assetSearchLookupAccessService.CallCount);
        Assert.Equal(1, sceneTreeLiteAccessService.CallCount);
        Assert.True(sceneTreeLiteAccessService.CapturedFailFast);
        Assert.Equal(UcliCommandIds.Query, sceneTreeLiteAccessService.CapturedCommand);
        Assert.Equal(0, unityRequestExecutor.CallCount);

        var opResult = Assert.Single(result.OpResults);
        var payload = opResult.Result!.Value;
        Assert.Equal("Assets/Scenes/Main.unity", payload.GetProperty("path").GetString());
        Assert.Equal(1, payload.GetProperty("roots").GetArrayLength());
        Assert.True(payload.GetProperty("window").GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityOnlyQuery_SendsQueryExecuteRequest ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var assetSearchLookupAccessService = new StubAssetSearchLookupAccessService();
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService();
        var unityRequestExecutor = new SpyUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var args = JsonSerializer.SerializeToElement(new
        {
            type = "UnityEngine.Transform, UnityEngine.CoreModule",
        });
        var result = await service.Execute(
            CreateInput(
                new QueryUnityOperationRequest(
                    CommandName: "query.comp.schema",
                    OperationId: "comp.schema",
                    OperationName: UcliPrimitiveOperationNames.CompSchema,
                    Args: args),
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, assetSearchLookupAccessService.CallCount);
        Assert.Equal(0, sceneTreeLiteAccessService.CallCount);
        Assert.Equal(1, unityRequestExecutor.CallCount);
        Assert.Equal(UcliCommandIds.Query, unityRequestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Oneshot, unityRequestExecutor.CapturedMode);
        Assert.True(result.ReadIndex.FallbackReason == "query operation is not backed by readIndex.");

        var executeRequest = Assert.IsType<UnityRequestPayload.ExecuteOperation>(unityRequestExecutor.CapturedPayload);
        Assert.Equal(UcliCommandIds.Query, executeRequest.Command);
        Assert.True(executeRequest.FailFast);
        Assert.Equal(result.RequestId, executeRequest.RequestId);
        Assert.Equal("comp.schema", executeRequest.OperationId);
        Assert.Equal(UcliPrimitiveOperationNames.CompSchema, executeRequest.OperationName);
        Assert.Equal("UnityEngine.Transform, UnityEngine.CoreModule", executeRequest.Args.GetProperty("type").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityOnlyQueryFailureHasBlankBoundaryMessage_NormalizesFailureMessage ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var assetSearchLookupAccessService = new StubAssetSearchLookupAccessService();
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService();
        var unityRequestExecutor = new SpyUnityRequestExecutor(UnityRequestExecutionResult.Failure("", ""));
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);
        var args = JsonSerializer.SerializeToElement(new
        {
            type = "UnityEngine.Transform, UnityEngine.CoreModule",
        });

        var result = await service.Execute(
            CreateInput(
                new QueryUnityOperationRequest(
                    CommandName: "query.comp.schema",
                    OperationId: "comp.schema",
                    OperationName: UcliPrimitiveOperationNames.CompSchema,
                    Args: args),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal("Request execution failed.", result.Message);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("Request execution failed.", error.Message);
    }

    private static QueryCommandInput CreateInput (
        QueryOperationRequest operation,
        ReadIndexMode? readIndexMode = null,
        bool failFast = false)
    {
        return new QueryCommandInput(
            ProjectPath: "/repo/UnityProject",
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 1234,
            ReadIndexMode: readIndexMode,
            FailFast: failFast,
            Operation: operation);
    }

    private static ProjectContext CreateContext ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1234,
        };
        return new ProjectContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Config: config,
            ConfigSource: ConfigSource.Default);
    }

    private static UnityRequestResponse CreateUnityResponse ()
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "unity-response-request-id",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: "comp.schema",
                    Op: UcliPrimitiveOperationNames.CompSchema,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        type = "UnityEngine.Transform, UnityEngine.CoreModule",
                    }),
                },
            ])),
            Errors: []));
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubAssetSearchLookupAccessService : IAssetSearchLookupAccessService
    {
        private readonly AssetSearchLookupReadResult result;

        public StubAssetSearchLookupAccessService ()
            : this(AssetSearchLookupReadResult.Failure("Asset lookup should not be read.", IpcErrorCodes.InternalError))
        {
        }

        public StubAssetSearchLookupAccessService (AssetSearchLookupReadResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public bool CapturedFailFast { get; private set; }

        public AssetSearchLookupQuery? CapturedQuery { get; private set; }

        public ValueTask<AssetSearchLookupReadResult> Search (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            ReadIndexMode readIndexMode,
            AssetSearchLookupQuery query,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedFailFast = failFast;
            CapturedQuery = query;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubSceneTreeLiteAccessService : ISceneTreeLiteAccessService
    {
        private readonly SceneTreeLiteReadResult result;

        public StubSceneTreeLiteAccessService ()
            : this(SceneTreeLiteReadResult.Failure("Scene tree should not be read.", IpcErrorCodes.InternalError))
        {
        }

        public StubSceneTreeLiteAccessService (SceneTreeLiteReadResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public UcliCommand CapturedCommand { get; private set; }

        public bool CapturedFailFast { get; private set; }

        public ValueTask<SceneTreeLiteReadResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            ReadIndexMode readIndexMode,
            string scenePath,
            int? depth,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedCommand = command;
            CapturedFailFast = failFast;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly Queue<UnityRequestExecutionResult> results;

        public SpyUnityRequestExecutor (params UnityRequestExecutionResult[] results)
        {
            this.results = new Queue<UnityRequestExecutionResult>(results ?? throw new ArgumentNullException(nameof(results)));
        }

        public int CallCount { get; private set; }

        public UcliCommand CapturedCommand { get; private set; }

        public UnityExecutionMode CapturedMode { get; private set; }

        public UnityRequestPayload? CapturedPayload { get; private set; }

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedCommand = command;
            CapturedMode = mode;
            CapturedPayload = payload;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("No queued Unity request execution result is available.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
