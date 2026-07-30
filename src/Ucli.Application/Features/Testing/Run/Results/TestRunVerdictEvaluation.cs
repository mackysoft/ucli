namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Holds normalized Unity test results together with the policy input and verdict derived from them. </summary>
internal sealed record TestRunVerdictEvaluation
{
    private TestRunVerdictEvaluation (
        UnityResultsXmlParseResult normalizedResult,
        bool allowEmptyTestRun)
    {
        ArgumentNullException.ThrowIfNull(normalizedResult);

        NormalizedResult = normalizedResult;
        AllowEmptyTestRun = allowEmptyTestRun;
        Verdict = CalculateVerdict(normalizedResult, allowEmptyTestRun);
    }

    /// <summary> Gets the normalized Unity test result that supplies all verdict evidence. </summary>
    public UnityResultsXmlParseResult NormalizedResult { get; }

    /// <summary> Gets the verdict established from normalized result evidence. </summary>
    public Verdict Verdict { get; }

    /// <summary> Gets a value indicating whether an empty normalized result set satisfies the requested condition. </summary>
    public bool AllowEmptyTestRun { get; }

    /// <summary>
    /// Evaluates one normalized result set under the explicitly requested empty-run policy.
    /// </summary>
    public static TestRunVerdictEvaluation Evaluate (
        UnityResultsXmlParseResult normalizedResult,
        bool allowEmptyTestRun)
    {
        return new TestRunVerdictEvaluation(normalizedResult, allowEmptyTestRun);
    }

    private static Verdict CalculateVerdict (
        UnityResultsXmlParseResult normalizedResult,
        bool allowEmptyTestRun)
    {
        if (normalizedResult.Counts.Failed > 0)
        {
            return Verdict.Fail;
        }

        if (normalizedResult.Counts.Skipped > 0
            || normalizedResult.Counts.Inconclusive > 0
            || (normalizedResult.ReportedTestCaseCount == 0 && !allowEmptyTestRun))
        {
            return Verdict.Incomplete;
        }

        return Verdict.Pass;
    }
}
