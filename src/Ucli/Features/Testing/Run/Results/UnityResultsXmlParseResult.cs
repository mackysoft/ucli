namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Represents parsed Unity results XML values used for JSON artifact generation. </summary>
/// <param name="Counts"> The aggregated count values. </param>
/// <param name="Tests"> The per-test entries. </param>
/// <param name="TopFailures"> The top failure entries. </param>
/// <param name="HasSuiteFailure"> Indicates whether XML includes failed suite-level result signals. </param>
internal sealed record UnityResultsXmlParseResult (
    UnityResultsXmlParseResult.CountsValue Counts,
    IReadOnlyList<UnityResultsXmlParseResult.TestValue> Tests,
    IReadOnlyList<UnityResultsXmlParseResult.TopFailureValue> TopFailures,
    bool HasSuiteFailure)
{
    /// <summary> Gets a value indicating whether parsed results contain failed tests. </summary>
    public bool HasFailedTests => Counts.Failed > 0 || HasSuiteFailure;

    /// <summary> Represents schema-compliant aggregated counts values. </summary>
    /// <param name="Passed"> The passed-test count. </param>
    /// <param name="Failed"> The failed-test count. </param>
    /// <param name="Skipped"> The skipped-test count. </param>
    internal sealed record CountsValue (
        int Passed,
        int Failed,
        int Skipped);

    /// <summary> Represents one per-test results entry. </summary>
    /// <param name="FullName"> The fully qualified test name. </param>
    /// <param name="Outcome"> The normalized outcome value. </param>
    /// <param name="DurationMs"> The test duration in milliseconds. </param>
    /// <param name="Categories"> The distinct category values preserving XML order. </param>
    internal sealed record TestValue (
        string FullName,
        string Outcome,
        int DurationMs,
        string[] Categories);

    /// <summary> Represents one top-failure entry for summary output. </summary>
    /// <param name="FullName"> The fully qualified test name. </param>
    /// <param name="Message"> The failure message. </param>
    /// <param name="StackTrace"> The failure stack trace. </param>
    internal sealed record TopFailureValue (
        string FullName,
        string Message,
        string StackTrace);
}
