using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceDepthTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDepthIsZero_RemovesAllChildren ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "depth-zero");
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
        var service = CreateService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), new UnexpectedSceneTreeLiteSourceRefreshService(), new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: 0,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Output!.Roots[0].Children!);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDepthIsNull_ReturnsFullTree ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "depth-null");
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
        var service = CreateService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), new UnexpectedSceneTreeLiteSourceRefreshService(), new RecordingSceneTreeLiteSourceProbe());

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
        Assert.Single(result.Output!.Roots[0].Children!);
        Assert.Single(result.Output.Roots[0].Children![0].Children!);
    }
}
