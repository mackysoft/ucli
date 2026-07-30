namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Represents a completed Unity results conversion. </summary>
internal sealed record UnityResultsConversionSuccess : UnityResultsConversionResult
{
    /// <summary> Initializes one completed Unity results conversion. </summary>
    /// <param name="verdictEvaluation">
    /// The normalized result, policy input, and verdict established by the conversion.
    /// </param>
    public UnityResultsConversionSuccess (TestRunVerdictEvaluation verdictEvaluation)
    {
        VerdictEvaluation = verdictEvaluation
            ?? throw new ArgumentNullException(nameof(verdictEvaluation));
    }

    /// <summary> Gets the normalized result, policy input, and verdict established by the conversion. </summary>
    public TestRunVerdictEvaluation VerdictEvaluation { get; }

    /// <summary> Gets the verdict derived from converted results. </summary>
    public Verdict Verdict => VerdictEvaluation.Verdict;

    /// <summary> Gets the test-case count derived from the same normalized result evidence. </summary>
    public int ReportedTestCaseCount =>
        VerdictEvaluation.NormalizedResult.ReportedTestCaseCount;
}
