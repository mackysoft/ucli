using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

namespace MackySoft.Ucli.Tests.Features.Requests.Resolve.UseCases.Resolve;

public sealed class ResolveServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchySelectorMatchesIndex_ReturnsGlobalObjectIdWithoutUnityFallback ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService(SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                ScenePath: "Assets/Scenes/Main.unity",
                Roots:
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        Name: "Root",
                        GlobalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
                        Children:
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                Name: "Child",
                                GlobalObjectId: "GlobalObjectId_V1-7-8-9-10-11-12",
                                Children: []),
                        ]),
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
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(0, unityRequestExecutor.CallCount);
        Assert.Equal(1, sceneTreeLiteAccessService.CallCount);
        Assert.Equal("Assets/Scenes/Main.unity", sceneTreeLiteAccessService.CapturedScenePath);
        Assert.Equal(ReadIndexMode.AllowStale, sceneTreeLiteAccessService.CapturedReadIndexMode);
        Assert.True(sceneTreeLiteAccessService.CapturedFailFast);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoTextCodec.SourceIndex, result.ReadIndex.Source);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessFresh, result.ReadIndex.Freshness);

        var opResult = Assert.Single(result.OpResults);
        Assert.Equal("resolve", opResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhaseNames.Plan, opResult.Phase);
        Assert.False(opResult.Applied);
        Assert.False(opResult.Changed);
        Assert.True(opResult.Result.HasValue);
        Assert.Equal("GlobalObjectId_V1-7-8-9-10-11-12", opResult.Result!.Value.GetProperty("globalObjectId").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchyIndexHasDuplicateAncestorNamesButFinalMatchIsUnique_ReturnsIndexResult ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService(SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                ScenePath: "Assets/Scenes/Main.unity",
                Roots:
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        Name: "Root",
                        GlobalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
                        Children:
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                Name: "Child",
                                GlobalObjectId: "GlobalObjectId_V1-7-8-9-10-11-12",
                                Children: []),
                        ]),
                    new IndexSceneTreeLiteNodeJsonContract(
                        Name: "Root",
                        GlobalObjectId: "GlobalObjectId_V1-13-14-15-16-17-18",
                        Children:
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                Name: "Other",
                                GlobalObjectId: "GlobalObjectId_V1-19-20-21-22-23-24",
                                Children: []),
                        ]),
                ],
                AccessInfo: new SceneTreeLiteAccessInfo(
                    Used: true,
                    Hit: true,
                    Source: SceneTreeLiteSource.Index,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                    FallbackReason: null)),
            "Scene-tree-lite read completed."));
        var unityRequestExecutor = new SpyUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, sceneTreeLiteAccessService.CallCount);
        Assert.Equal(0, unityRequestExecutor.CallCount);
        Assert.True(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoTextCodec.SourceIndex, result.ReadIndex.Source);

        var opResult = Assert.Single(result.OpResults);
        Assert.True(opResult.Result.HasValue);
        Assert.Equal("GlobalObjectId_V1-7-8-9-10-11-12", opResult.Result!.Value.GetProperty("globalObjectId").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSelectorRequiresUnityFallback_SendsResolveExecuteRequest ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var sceneTreeLiteAccessService = new StubSceneTreeLiteAccessService(
            SceneTreeLiteReadResult.Failure("Scene-tree-lite should not be read.", IpcErrorCodes.InternalError));
        var unityRequestExecutor = new SpyUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                selector: new ResolveAssetGuidSelectorInput("11111111111111111111111111111111"),
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, sceneTreeLiteAccessService.CallCount);
        Assert.Equal(1, unityRequestExecutor.CallCount);
        Assert.Equal(UcliCommandIds.Resolve, unityRequestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Oneshot, unityRequestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), unityRequestExecutor.CapturedTimeout);
        Assert.Equal(IpcMethodNames.Execute, unityRequestExecutor.CapturedMethod);
        Assert.False(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoTextCodec.SourceUnity, result.ReadIndex.Source);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessFresh, result.ReadIndex.Freshness);
        Assert.Equal("selector requires live Unity resolution.", result.ReadIndex.FallbackReason);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            unityRequestExecutor.CapturedPayload,
            out IpcExecuteRequest executeRequest,
            out var payloadError));
        Assert.Equal(IpcPayloadReadError.None, payloadError);
        Assert.Equal(UcliCommandIds.Resolve, executeRequest.Command);
        Assert.True(executeRequest.FailFast);
        Assert.Equal(result.RequestId, executeRequest.Arguments.GetProperty("requestId").GetString());
        var step = Assert.Single(executeRequest.Arguments.GetProperty("steps").EnumerateArray());
        Assert.Equal("resolve", step.GetProperty("id").GetString());
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, step.GetProperty("op").GetString());
        Assert.Equal("11111111111111111111111111111111", step.GetProperty("args").GetProperty("assetGuid").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchyIndexMisses_FallsBackToUnityWithReason ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
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
        var unityRequestExecutor = new SpyUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.Execute(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, sceneTreeLiteAccessService.CallCount);
        Assert.Equal(1, unityRequestExecutor.CallCount);
        Assert.Equal(ReadIndexInfoTextCodec.SourceUnity, result.ReadIndex.Source);
        Assert.False(result.ReadIndex.Used);
        Assert.Equal("Hierarchy path 'Root/Child' did not match a GameObject.", result.ReadIndex.FallbackReason);
    }

    private static ResolveCommandInput CreateInput (
        ResolveSelectorInput selector,
        ReadIndexMode? readIndexMode = null,
        bool failFast = false)
    {
        return new ResolveCommandInput(
            ProjectPath: "/repo/UnityProject",
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 1234,
            ReadIndexMode: readIndexMode,
            FailFast: failFast,
            Selector: selector);
    }

    private static ResolveSelectorInput CreateSceneSelector ()
    {
        return new ResolveSceneHierarchySelectorInput(
            Scene: "Assets/Scenes/Main.unity",
            HierarchyPath: "Root/Child");
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

    private static IpcResponse CreateUnityResponse ()
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "unity-response-request-id",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: "resolve",
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        globalObjectId = "GlobalObjectId_V1-1-2-3-4-5-6",
                    }),
                },
            ])),
            Errors: []);
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

    private sealed class StubSceneTreeLiteAccessService : ISceneTreeLiteAccessService
    {
        private readonly SceneTreeLiteReadResult result;

        public StubSceneTreeLiteAccessService (SceneTreeLiteReadResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public string? CapturedScenePath { get; private set; }

        public ReadIndexMode? CapturedReadIndexMode { get; private set; }

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
            CapturedScenePath = scenePath;
            CapturedReadIndexMode = readIndexMode;
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

        public TimeSpan CapturedTimeout { get; private set; }

        public string? CapturedMethod { get; private set; }

        public JsonElement CapturedPayload { get; private set; }

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedCommand = command;
            CapturedMode = mode;
            CapturedTimeout = timeout;
            CapturedMethod = method;
            CapturedPayload = payload;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("No queued Unity request execution result is available.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
