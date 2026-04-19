using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Evaluates read-index freshness and applies mode-specific freshness constraints. </summary>
internal sealed class IndexFreshnessEvaluator : IIndexFreshnessEvaluator
{
    private readonly IIndexInputFingerprintCalculator inputFingerprintCalculator;

    /// <summary> Initializes a new instance of the <see cref="IndexFreshnessEvaluator" /> class. </summary>
    /// <param name="inputFingerprintCalculator"> The input fingerprint calculator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public IndexFreshnessEvaluator (IIndexInputFingerprintCalculator inputFingerprintCalculator)
    {
        this.inputFingerprintCalculator = inputFingerprintCalculator ?? throw new ArgumentNullException(nameof(inputFingerprintCalculator));
    }

    /// <summary> Evaluates index freshness for one Unity project context. </summary>
    /// <param name="projectRoot"> The Unity project root path. </param>
    /// <param name="target"> The read-index target being evaluated. </param>
    /// <param name="persistedSourceInputsHash"> The persisted source-inputs hash stored on the target artifact. </param>
    /// <param name="mode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to freshness evaluation result. </returns>
    public async ValueTask<IndexFreshnessEvaluationResult> Evaluate (
        string projectRoot,
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

        if (string.IsNullOrWhiteSpace(persistedSourceInputsHash))
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        if (UsesCoreInputSnapshot(target))
        {
            var currentCoreSnapshot = await inputFingerprintCalculator.TryComputeCore(projectRoot, cancellationToken).ConfigureAwait(false);
            if (currentCoreSnapshot == null)
            {
                return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
            }

            var coreFreshness = IndexFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentCoreSnapshot, target);
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, coreFreshness);
        }

        var currentSnapshot = await inputFingerprintCalculator.TryCompute(projectRoot, cancellationToken).ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return IndexFreshnessPolicy.ApplyModeConstraint(mode, IndexFreshness.Probable);
        }

        var freshness = IndexFreshnessPolicy.EvaluateFreshness(persistedSourceInputsHash, currentSnapshot, target);
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
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported read-index freshness target."),
        };
    }
}