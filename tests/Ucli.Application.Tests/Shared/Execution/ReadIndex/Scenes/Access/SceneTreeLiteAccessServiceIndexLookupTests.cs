using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceIndexLookupTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenAllowStaleLookupExists_ReturnsTrimmedIndexRoots ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "index-depth-one");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new UnexpectedSceneTreeLiteSourceRefreshService();
        var service = CreateService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: 1,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Index, result.Output!.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.Equal(IndexFreshness.Probable, result.Output.AccessInfo.Freshness);
        Assert.Single(result.Output.Roots);
        Assert.Single(result.Output.Roots[0].Children!);
        Assert.Empty(result.Output.Roots[0].Children![0].Children!);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenState.NotExpandedByDepth, result.Output.Roots[0].Children[0].ChildrenState);
        SceneTreeLiteAccessInvocationAssert.FreshnessObservedFor(
            freshnessEvaluator,
            project,
            "Assets/Scenes/Main.unity",
            Sha256DigestTestFactory.Compute("scene-hash"));
    }
}
