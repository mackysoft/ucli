using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceInputValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenAssetsSceneIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "missing-scene");
        var project = CreateProject(scope);
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(),
        };
        var service = CreateService(
            indexReader,
            new RecordingReadIndexFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new UnexpectedSceneTreeLiteSourceRefreshService(),
            new RecordingSceneTreeLiteSourceProbe
            {
                Result = SceneTreeLiteSourceProbeResult.Failure("Scene path could not be resolved: Assets/Scenes/Main.unity"),
            });

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

        SceneTreeLiteAccessInvocationAssert.InvalidSceneRejectedBeforeIndexLookup(
            result,
            indexReader,
            "Scene path could not be resolved");
    }

}
