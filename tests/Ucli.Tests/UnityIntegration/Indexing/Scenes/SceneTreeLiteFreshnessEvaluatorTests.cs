using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteFreshnessEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsFresh_WhenPersistedHashMatchesCurrentHash ()
    {
        var calculator = new StubSceneTreeLiteSourceHashCalculator
        {
            Result = "scene-hash",
        };
        var evaluator = new SceneTreeLiteFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRootPath: "/repo/UnityProject",
            scenePath: "Assets/Scenes/Main.unity",
            persistedSourceInputsHash: "scene-hash",
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Fresh, result.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_ReturnsProbable_WhenCurrentHashCannotBeComputed ()
    {
        var calculator = new StubSceneTreeLiteSourceHashCalculator
        {
            Result = null,
        };
        var evaluator = new SceneTreeLiteFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRootPath: "/repo/UnityProject",
            scenePath: "Assets/Scenes/Main.unity",
            persistedSourceInputsHash: "scene-hash",
            mode: ReadIndexMode.AllowStale,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Probable, result.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Evaluate_WhenModeRequiresFreshAndHashDiffers_ReturnsFreshRequiredError ()
    {
        var calculator = new StubSceneTreeLiteSourceHashCalculator
        {
            Result = "current-hash",
        };
        var evaluator = new SceneTreeLiteFreshnessEvaluator(calculator);

        var result = await evaluator.Evaluate(
            projectRootPath: "/repo/UnityProject",
            scenePath: "Assets/Scenes/Main.unity",
            persistedSourceInputsHash: "persisted-hash",
            mode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, result.Error!.Code);
    }

    private sealed class StubSceneTreeLiteSourceHashCalculator : ISceneTreeLiteSourceHashCalculator
    {
        public string? Result { get; set; }

        public ValueTask<string?> TryCompute (
            string projectRootPath,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }
}
