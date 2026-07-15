using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceDirtySourceProbeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDirtyLoadedSourceProbeSucceeds_ReturnsSourceBeforeReadingIndex ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "dirty-probe-source");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var dirtySnapshot = CreateSceneTreeLiteSourceSnapshot(
            generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:02:00+00:00"),
            scenePath: "Assets/Scenes/Main.unity",
            roots:
            [
                new IndexSceneTreeLiteNodeJsonContract("DirtyRoot", "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenState.Complete),
            ],
            sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));
        var dirtyProbeService = new RecordingSceneTreeLiteDirtySourceProbeService
        {
            Result = SceneTreeLiteDirtySourceProbeResult.DirtySource(dirtySnapshot, "Dirty loaded scene is open in Unity daemon."),
        };
        var refreshService = new UnexpectedSceneTreeLiteSourceRefreshService();
        var service = CreateService(
            indexReader,
            new RecordingReadIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            refreshService,
            new RecordingSceneTreeLiteSourceProbe(),
            dirtyProbeService);

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        SceneTreeLiteAccessInvocationAssert.DirtyLoadedSourceReturnedBeforeIndexLookup(
            result,
            indexReader,
            dirtyProbeService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity",
            "DirtyRoot");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDirtyLoadedSourceProbeIsUnavailable_UsesIndex ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "dirty-probe-index");
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
        var dirtyProbeService = new RecordingSceneTreeLiteDirtySourceProbeService
        {
            Result = SceneTreeLiteDirtySourceProbeResult.NotAvailable("Unity daemon scene is not dirty loaded source."),
        };
        var service = CreateService(
            indexReader,
            freshnessEvaluator,
            new TestMutationReadPostconditionStore(),
            new UnexpectedSceneTreeLiteSourceRefreshService(),
            new RecordingSceneTreeLiteSourceProbe(),
            dirtyProbeService);

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Index, result.Output!.AccessInfo.Source);
        Assert.Equal(SceneTreeSourceStateKind.ReadIndex, result.Output.SourceState.Kind);
        Assert.False(result.Output.SourceState.IsDirty);
        SceneTreeLiteAccessInvocationAssert.DirtySourceProbeAttemptedFor(
            dirtyProbeService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity");
        SceneTreeLiteAccessInvocationAssert.SceneTreeLiteLookupReadFor(indexReader, project);
    }
}
