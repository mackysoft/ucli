using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Observes read-index freshness from persisted and current input fingerprints. </summary>
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
    public async ValueTask<IndexFreshnessEvaluationResult> ObserveAsync (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        Sha256Digest persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(persistedSourceInputsHash);

        if (target == IndexFreshnessTarget.OpsCatalog)
        {
            var currentCoreSnapshot = await inputFingerprintProvider.TryComputeCoreAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (currentCoreSnapshot == null)
            {
                return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
            }

            var coreFreshness = IndexHashFreshnessPolicy.EvaluateCoreFreshness(persistedSourceInputsHash, currentCoreSnapshot);
            return IndexFreshnessEvaluationResult.Success(coreFreshness);
        }

        var currentSnapshot = await inputFingerprintProvider.TryComputeAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentSnapshot, target);
        return IndexFreshnessEvaluationResult.Success(freshness);
    }

    /// <inheritdoc />
    public async ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLiteAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        Sha256Digest persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentNullException.ThrowIfNull(persistedSourceInputsHash);

        var currentSourceHash = await sceneSourceHashProvider.TryComputeAsync(unityProject, scenePath, cancellationToken).ConfigureAwait(false);
        if (currentSourceHash == null)
        {
            return IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable);
        }

        var freshness = IndexHashFreshnessPolicy.EvaluateSceneTreeLiteFreshness(persistedSourceInputsHash, currentSourceHash);
        return IndexFreshnessEvaluationResult.Success(freshness);
    }
}
