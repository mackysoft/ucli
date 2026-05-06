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
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var observedResult = await Observe(unityProject, target, persistedSourceInputsHash, cancellationToken).ConfigureAwait(false);
        if (!observedResult.IsSuccess)
        {
            return observedResult;
        }

        return IndexFreshnessPolicy.ApplyModeConstraint(mode, observedResult.Freshness);
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> Observe (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        if (string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        if (UsesCoreInputSnapshot(target))
        {
            var currentCoreSnapshot = await inputFingerprintProvider.TryComputeCore(unityProject, cancellationToken).ConfigureAwait(false);
            if (currentCoreSnapshot == null)
            {
                return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
            }

            var coreFreshness = IndexHashFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentCoreSnapshot, target);
            return IndexFreshnessEvaluationResult.Success(coreFreshness);
        }

        var currentSnapshot = await inputFingerprintProvider.TryCompute(unityProject, cancellationToken).ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentSnapshot, target);
        return IndexFreshnessEvaluationResult.Success(freshness);
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        string? persistedSourceInputsHash,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var observedResult = await ObserveSceneTreeLite(unityProject, scenePath, persistedSourceInputsHash, cancellationToken).ConfigureAwait(false);
        if (!observedResult.IsSuccess)
        {
            return observedResult;
        }

        return IndexFreshnessPolicy.ApplyModeConstraint(mode, observedResult.Freshness);
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLite (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        if (string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        var currentSourceHash = await sceneSourceHashProvider.TryCompute(unityProject, scenePath, cancellationToken).ConfigureAwait(false);
        if (currentSourceHash == null)
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateSceneTreeLiteFreshness(persistedSourceInputsHash, currentSourceHash);
        return IndexFreshnessEvaluationResult.Success(freshness);
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
