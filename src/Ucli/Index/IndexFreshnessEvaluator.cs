using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Index;

/// <summary> Evaluates read-index freshness and applies mode-specific freshness constraints. </summary>
internal sealed class IndexFreshnessEvaluator : IIndexFreshnessEvaluator
{
    private readonly IIndexCatalogReader catalogReader;
    private readonly IIndexInputFingerprintCalculator inputFingerprintCalculator;

    /// <summary> Initializes a new instance of the <see cref="IndexFreshnessEvaluator" /> class. </summary>
    /// <param name="catalogReader"> The index catalog reader dependency. </param>
    /// <param name="inputFingerprintCalculator"> The input fingerprint calculator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public IndexFreshnessEvaluator (
        IIndexCatalogReader catalogReader,
        IIndexInputFingerprintCalculator inputFingerprintCalculator)
    {
        this.catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
        this.inputFingerprintCalculator = inputFingerprintCalculator ?? throw new ArgumentNullException(nameof(inputFingerprintCalculator));
    }

    /// <summary> Evaluates index freshness for one Unity project context. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="projectRoot"> The Unity project root path. </param>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to freshness evaluation result. </returns>
    public async ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string storageRoot,
        string projectFingerprint,
        string projectRoot,
        ReadIndexMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == ReadIndexMode.Disabled)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var manifestReadResult = await catalogReader.ReadInputsManifest(storageRoot, projectFingerprint, cancellationToken).ConfigureAwait(false);
        if (!manifestReadResult.IsSuccess)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var currentSnapshot = await inputFingerprintCalculator.TryCompute(projectRoot, cancellationToken).ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var manifest = manifestReadResult.Value!;
        var freshness = IndexFreshnessPolicy.EvaluateFreshness(manifest, currentSnapshot);
        return IndexFreshnessPolicy.ApplyModeConstraint(mode, freshness);
    }
}