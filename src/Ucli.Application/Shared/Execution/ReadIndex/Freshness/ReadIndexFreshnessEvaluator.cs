using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Evaluates read-index freshness and applies mode-specific freshness constraints. </summary>
internal sealed class ReadIndexFreshnessEvaluator : IReadIndexFreshnessEvaluator
{
    private readonly IReadIndexInputFingerprintProvider inputFingerprintProvider;

    private readonly IReadIndexSceneSourceHashProvider sceneSourceHashProvider;

    /// <summary> Initializes a new instance of the <see cref="ReadIndexFreshnessEvaluator" /> class. </summary>
    public ReadIndexFreshnessEvaluator (
        IReadIndexInputFingerprintProvider inputFingerprintProvider,
        IReadIndexSceneSourceHashProvider sceneSourceHashProvider)
    {
        this.inputFingerprintProvider = inputFingerprintProvider ?? throw new ArgumentNullException(nameof(inputFingerprintProvider));
        this.sceneSourceHashProvider = sceneSourceHashProvider ?? throw new ArgumentNullException(nameof(sceneSourceHashProvider));
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRootPath,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled || string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        if (UsesCoreInputSnapshot(target))
        {
            var currentCoreSnapshot = await inputFingerprintProvider.TryComputeCore(projectRootPath, cancellationToken).ConfigureAwait(false);
            if (currentCoreSnapshot == null)
            {
                return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
            }

            var coreFreshness = IndexHashFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentCoreSnapshot, target);
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, coreFreshness);
        }

        var currentSnapshot = await inputFingerprintProvider.TryCompute(projectRootPath, cancellationToken).ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentSnapshot, target);
        return IndexFreshnessPolicy.ApplyModeConstraint(mode, freshness);
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
        string projectRootPath,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled || string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var currentSourceHash = await sceneSourceHashProvider.TryCompute(projectRootPath, scenePath, cancellationToken).ConfigureAwait(false);
        if (currentSourceHash == null)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateSceneTreeLiteFreshness(persistedSourceInputsHash, currentSourceHash);
        return IndexFreshnessPolicy.ApplyModeConstraint(mode, freshness);
    }

    private static bool UsesCoreInputSnapshot (IndexFreshnessTarget target)
    {
        return target switch
        {
            IndexFreshnessTarget.OpsCatalog => true,
            IndexFreshnessTarget.TypesCatalog => true,
            IndexFreshnessTarget.SchemasCatalog => true,
            IndexFreshnessTarget.AssetSearchLookup => false,
            IndexFreshnessTarget.GuidPathLookup => false,
            IndexFreshnessTarget.SceneTreeLite => throw new ArgumentOutOfRangeException(nameof(target), target, "Scene-tree-lite freshness must be evaluated with scene source hash."),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }
}
