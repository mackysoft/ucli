using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServicePostconditionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenReadPostconditionRequiresNewerSceneIndex_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "postcondition-fallback");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                OperationExecutionModelMapper.MapReadPostcondition(new IpcExecuteReadPostcondition(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"))
                    {
                        ScenePath = "Assets/Scenes/Main.unity",
                    },
                ]))!),
        };
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("FreshRoot", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "Existing scene-tree-lite index generatedAtUtc is older than mutation read postcondition."),
        };
        var service = CreateService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenReadPostconditionTargetsAllScenes_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "postcondition-wildcard-fallback");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                OperationExecutionModelMapper.MapReadPostcondition(new IpcExecuteReadPostcondition(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:00:00+00:00")),
                ]))!),
        };
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("FreshRoot", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "Existing scene-tree-lite index generatedAtUtc is older than mutation read postcondition."),
        };
        var service = CreateService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenReadPostconditionTargetsDifferentScene_KeepsUsingIndex ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "postcondition-non-matching-scene");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                OperationExecutionModelMapper.MapReadPostcondition(new IpcExecuteReadPostcondition(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"))
                    {
                        ScenePath = "Assets/Scenes/Other.unity",
                    },
                ]))!),
        };
        var refreshService = new UnexpectedSceneTreeLiteSourceRefreshService();
        var service = CreateService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Index, result.Output!.AccessInfo.Source);
    }
}
