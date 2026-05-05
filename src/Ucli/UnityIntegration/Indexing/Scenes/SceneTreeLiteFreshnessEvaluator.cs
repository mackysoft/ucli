using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Evaluates scene-tree-lite freshness and applies read-index mode constraints. </summary>
internal sealed class SceneTreeLiteFreshnessEvaluator : ISceneTreeLiteFreshnessEvaluator
{
    private readonly ISceneTreeLiteSourceHashCalculator sourceHashCalculator;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteFreshnessEvaluator" /> class. </summary>
    public SceneTreeLiteFreshnessEvaluator (ISceneTreeLiteSourceHashCalculator sourceHashCalculator)
    {
        this.sourceHashCalculator = sourceHashCalculator ?? throw new ArgumentNullException(nameof(sourceHashCalculator));
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRootPath,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled)
        {
            return IndexHashFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        if (string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexHashFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var currentSourceHash = await sourceHashCalculator.TryCompute(projectRootPath, scenePath, cancellationToken).ConfigureAwait(false);
        if (currentSourceHash == null)
        {
            return IndexHashFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var freshness = string.Equals(persistedSourceInputsHash, currentSourceHash, StringComparison.Ordinal)
            ? IndexFreshness.Fresh
            : IndexFreshness.Stale;
        return IndexHashFreshnessPolicy.ApplyModeConstraint(mode, freshness);
    }
}
