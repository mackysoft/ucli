namespace MackySoft.Ucli.Shared.Execution.ReadPostcondition;

/// <summary> Represents one read-index usability decision after applying mutation read postconditions. </summary>
/// <param name="CanUseIndex"> Whether the persisted index may be used safely. </param>
/// <param name="FallbackReason"> The reason the caller must fall back to live source when index use is unsafe. </param>
internal sealed record MutationReadPostconditionEvaluationResult (
    bool CanUseIndex,
    string? FallbackReason)
{
    /// <summary> Creates an allow decision. </summary>
    public static MutationReadPostconditionEvaluationResult Allow ()
    {
        return new MutationReadPostconditionEvaluationResult(true, null);
    }

    /// <summary> Creates a reject decision. </summary>
    public static MutationReadPostconditionEvaluationResult Reject (string fallbackReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        return new MutationReadPostconditionEvaluationResult(false, fallbackReason);
    }
}
