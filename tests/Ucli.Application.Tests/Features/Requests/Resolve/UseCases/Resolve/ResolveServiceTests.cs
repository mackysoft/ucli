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
    private const string RootGlobalObjectId = "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0";
    private const string ChildGlobalObjectId = "GlobalObjectId_V1-2-11111111111111111111111111111111-2-0";

    private static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

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
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity"),
                    Roots:
                    [
                        CreateSceneNode(
                            "Root",
                            RootGlobalObjectId,
                            [
                                CreateSceneNode("Child", ChildGlobalObjectId),
                            ]),
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
            RequestId,
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
        Assert.Equal("resolve", opResult.OpId.Value);
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhase.Plan, opResult.Phase);
        Assert.False(opResult.Applied);
        Assert.False(opResult.Changed);
        Assert.True(opResult.Result.HasValue);
        Assert.Equal(ChildGlobalObjectId, opResult.Result!.Value.GetProperty("globalObjectId").GetString());
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
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity"),
                    Roots:
                    [
                        CreateSceneNode(
                            "Root",
                            RootGlobalObjectId,
                            [
                                CreateSceneNode("Child", ChildGlobalObjectId),
                            ]),
                        CreateSceneNode(
                            "Root",
                            "GlobalObjectId_V1-2-11111111111111111111111111111111-3-0",
                            [
                                CreateSceneNode("Other", "GlobalObjectId_V1-2-11111111111111111111111111111111-4-0"),
                            ]),
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
            RequestId,
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
        Assert.Equal(ChildGlobalObjectId, opResult.Result!.Value.GetProperty("globalObjectId").GetString());
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
            RequestId,
            CreateInput(
                selector: new ResolveAssetGuidSelectorInput(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                failFast: true),
            CancellationToken.None);

        RequestReadIndexAccessInvocationAssert.ResolveSelectorBypassedSceneTreeLiteAccess(
            result,
            sceneTreeLiteAccessService);
        Assert.NotNull(result.Project);
        var project = result.Project!;
        Assert.Equal(ResolveProjectContext.UnityProject.UnityProjectRoot, project.ProjectPath);
        Assert.Equal(ResolveProjectContext.UnityProject.ProjectFingerprint, project.ProjectFingerprint);
        Assert.Equal(ResolveProjectContext.UnityProject.UnityVersion, project.UnityVersion);
        Assert.Equal(RequestId, result.RequestId);

        var execution = RequestReadIndexAccessInvocationAssert.UnityOperationRequestedOnce(
            unityRequestExecutor,
            UcliCommandIds.Resolve,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: true,
            expectedOperationId: "resolve",
            expectedOperationName: UcliPrimitiveOperationNames.Resolve);
        var executeRequest = execution.Request;
        Assert.Equal("11111111-1111-1111-1111-111111111111", executeRequest.Args.GetProperty("assetGuid").GetString());
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
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity"),
                    Roots:
                    [
                        CreateSceneNode("Root", RootGlobalObjectId),
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
            RequestId,
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
            expectedOperationId: "resolve",
            expectedOperationName: UcliPrimitiveOperationNames.Resolve);
        Assert.Equal(ReadIndexInfoSource.Unity, result.ReadIndex.Source);
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
            scene: new SceneAssetPath("Assets/Scenes/Main.unity"),
            hierarchyPath: new UnityHierarchyPath("Root/Child"));
    }

    private static SceneTreeLiteNode CreateSceneNode (
        string name,
        string globalObjectId,
        IReadOnlyList<SceneTreeLiteNode>? children = null)
    {
        return new SceneTreeLiteNode(
            name,
            new UnityGlobalObjectId(globalObjectId),
            children ?? [],
            IndexSceneTreeLiteNodeChildrenState.Complete);
    }

    private static UnityRequestResponse CreateUnityResponse ()
    {
        return ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcResponseStatus.Ok,
            opResults:
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("resolve"),
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        globalObjectId = RootGlobalObjectId,
                    }),
                },
            ],
            errors: [],
            project: CreateUnityResponseProjectIdentity());
    }

    private static IpcProjectIdentity CreateUnityResponseProjectIdentity ()
    {
        return new IpcProjectIdentity(
            projectPath: ResolveProjectContext.UnityProject.UnityProjectRoot,
            projectFingerprint: ResolveProjectContext.UnityProject.ProjectFingerprint,
            unityVersion: ResolveProjectContext.UnityProject.UnityVersion);
    }

}
