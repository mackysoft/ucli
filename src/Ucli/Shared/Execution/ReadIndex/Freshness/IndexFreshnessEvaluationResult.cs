using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Shared.Execution.ReadIndex;

/// <summary> Represents one index freshness evaluation result. </summary>
/// <param name="Freshness"> The evaluated freshness value. </param>
/// <param name="Error"> The structured error when evaluation fails; otherwise <see langword="null" />. </param>
internal sealed record IndexFreshnessEvaluationResult (
    IndexFreshness Freshness,
    IndexServiceError? Error)
{
    /// <summary> Gets a value indicating whether freshness evaluation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful freshness evaluation result. </summary>
    /// <param name="freshness"> The freshness value. </param>
    /// <returns> The successful result. </returns>
    public static IndexFreshnessEvaluationResult Success (IndexFreshness freshness)
    {
        return new IndexFreshnessEvaluationResult(freshness, null);
    }

    /// <summary> Creates a failed freshness evaluation result. </summary>
    /// <param name="freshness"> The last known freshness value. </param>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static IndexFreshnessEvaluationResult Failure (
        IndexFreshness freshness,
        IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new IndexFreshnessEvaluationResult(freshness, error);
    }
}