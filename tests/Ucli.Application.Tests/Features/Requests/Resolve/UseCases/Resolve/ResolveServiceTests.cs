using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ResolveServiceTests
{
    private static readonly ProjectContext ResolveProjectContext = ProjectContextTestFactory.CreateRepositoryFixtureProject(
        UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1234,
        });

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchySelectorMatchesIndex_ReturnsGlobalObjectIdWithoutUnityFallback ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ResolveProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = SceneTreeLiteReadResult.Success(
                new SceneTreeLiteReadOutput(
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Root",
                            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
                            children:
                            [
                                new IndexSceneTreeLiteNodeJsonContract(
                                    name: "Child",
                                    globalObjectId: "GlobalObjectId_V1-7-8-9-10-11-12",
                                    children: [],
                                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                            ],
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                    AccessInfo: new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: IndexFreshness.Fresh,
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                        FallbackReason: null)),
                "Scene-tree-lite read completed."),
        };
        var unityRequestExecutor = new UnexpectedUnityRequestExecutor();
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        RequestReadIndexAccessInvocationAssert.SceneTreeRequestedOnce(
            sceneTreeLiteAccessService,
            UcliCommandIds.Resolve,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedReadIndexMode: ReadIndexMode.AllowStale,
            expectedFailFast: true);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoSource.Index, result.ReadIndex.Source);
        Assert.Equal(IndexFreshness.Fresh, result.ReadIndex.Freshness);

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
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ResolveProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = SceneTreeLiteReadResult.Success(
                new SceneTreeLiteReadOutput(
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Root",
                            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
                            children:
                            [
                                new IndexSceneTreeLiteNodeJsonContract(
                                    name: "Child",
                                    globalObjectId: "GlobalObjectId_V1-7-8-9-10-11-12",
                                    children: [],
                                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                            ],
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Root",
                            globalObjectId: "GlobalObjectId_V1-13-14-15-16-17-18",
                            children:
                            [
                                new IndexSceneTreeLiteNodeJsonContract(
                                    name: "Other",
                                    globalObjectId: "GlobalObjectId_V1-19-20-21-22-23-24",
                                    children: [],
                                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                            ],
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                    AccessInfo: new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: IndexFreshness.Fresh,
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                        FallbackReason: null)),
                "Scene-tree-lite read completed."),
        };
        var unityRequestExecutor = new UnexpectedUnityRequestExecutor();
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        RequestReadIndexAccessInvocationAssert.SceneTreeRequestedOnce(
            sceneTreeLiteAccessService,
            UcliCommandIds.Resolve,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedReadIndexMode: ReadIndexMode.AllowStale,
            expectedFailFast: false);
        Assert.True(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoSource.Index, result.ReadIndex.Source);

        var opResult = Assert.Single(result.OpResults);
        Assert.True(opResult.Result.HasValue);
        Assert.Equal("GlobalObjectId_V1-7-8-9-10-11-12", opResult.Result!.Value.GetProperty("globalObjectId").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSelectorRequiresUnityFallback_SendsResolveExecuteRequest ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ResolveProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService();
        var unityRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                selector: new ResolveAssetGuidSelectorInput("11111111111111111111111111111111"),
                failFast: true),
            CancellationToken.None);

        RequestReadIndexAccessInvocationAssert.ResolveSelectorBypassedSceneTreeLiteAccess(
            result,
            sceneTreeLiteAccessService);
        Assert.NotNull(result.Project);
        var project = result.Project!;
        Assert.Equal("/unity/ResponseProject", project.ProjectPath);
        Assert.Equal("unity-response-fingerprint", project.ProjectFingerprint);
        Assert.Equal("7000.0.1f1", project.UnityVersion);

        var execution = RequestReadIndexAccessInvocationAssert.UnityOperationRequestedOnce(
            unityRequestExecutor,
            UcliCommandIds.Resolve,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: true,
            expectedRequestId: result.RequestId,
            expectedOperationId: "resolve",
            expectedOperationName: UcliPrimitiveOperationNames.Resolve);
        var executeRequest = execution.Request;
        Assert.Equal("11111111111111111111111111111111", executeRequest.Args.GetProperty("assetGuid").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchyIndexMisses_FallsBackToUnityWithReason ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ResolveProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = SceneTreeLiteReadResult.Success(
                new SceneTreeLiteReadOutput(
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("Root", "GlobalObjectId_V1-1-2-3-4-5-6", [], IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                    AccessInfo: new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: IndexFreshness.Fresh,
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                        FallbackReason: null)),
                "Scene-tree-lite read completed."),
        };
        var unityRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        RequestReadIndexAccessInvocationAssert.SceneTreeRequestedOnce(
            sceneTreeLiteAccessService,
            UcliCommandIds.Resolve,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedReadIndexMode: ReadIndexMode.AllowStale,
            expectedFailFast: false);
        RequestReadIndexAccessInvocationAssert.UnityOperationRequestedOnce(
            unityRequestExecutor,
            UcliCommandIds.Resolve,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: false,
            expectedRequestId: result.RequestId,
            expectedOperationId: "resolve",
            expectedOperationName: UcliPrimitiveOperationNames.Resolve);
        Assert.Equal(ReadIndexInfoSource.Unity, result.ReadIndex.Source);
        Assert.False(result.ReadIndex.Used);
        Assert.Equal("Hierarchy path 'Root/Child' did not match a GameObject.", result.ReadIndex.FallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSceneHierarchyIndexFailureMessageIsBlank_FallsBackToUnityWithDefaultReason ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ResolveProjectContext));
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService
        {
            Result = SceneTreeLiteReadResult.Failure("", UcliCoreErrorCodes.InternalError),
        };
        var unityRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new ResolveService(projectContextResolver, sceneTreeLiteAccessService, unityRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                selector: CreateSceneSelector(),
                readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        RequestReadIndexAccessInvocationAssert.SceneTreeRequestedOnce(
            sceneTreeLiteAccessService,
            UcliCommandIds.Resolve,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedReadIndexMode: ReadIndexMode.AllowStale,
            expectedFailFast: false);
        RequestReadIndexAccessInvocationAssert.UnityOperationRequestedOnce(
            unityRequestExecutor,
            UcliCommandIds.Resolve,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: false,
            expectedRequestId: result.RequestId,
            expectedOperationId: "resolve",
            expectedOperationName: UcliPrimitiveOperationNames.Resolve);
        Assert.Equal(ReadIndexInfoSource.Unity, result.ReadIndex.Source);
        Assert.Equal("readIndex fallback reason is unavailable.", result.ReadIndex.FallbackReason);
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

    private static UnityRequestResponse CreateUnityResponse ()
    {
        return ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcProtocol.StatusOk,
            opResults:
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
            ],
            errors: [],
            project: CreateUnityResponseProjectIdentity());
    }

    private static IpcProjectIdentity CreateUnityResponseProjectIdentity ()
    {
        return new IpcProjectIdentity(
            ProjectPath: "/unity/ResponseProject",
            ProjectFingerprint: "unity-response-fingerprint",
            UnityVersion: "7000.0.1f1");
    }

}
