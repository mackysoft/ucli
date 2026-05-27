namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Represents one Unity results conversion result. </summary>
/// <param name="HasFailedTests"> Indicates whether converted results contain failing tests when successful; otherwise <see langword="false" />. </param>
/// <param name="ReportedTestCaseCount"> The number of test cases reported by Unity results XML when successful; otherwise <c>0</c>. </param>
/// <param name="FailureKind"> The conversion failure kind on failure; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The user-facing conversion failure message on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityResultsConversionResult (
    bool HasFailedTests,
    int ReportedTestCaseCount,
    UnityResultsConversionFailureKind? FailureKind,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether conversion succeeded. </summary>
    public bool IsSuccess => FailureKind is null;

    /// <summary> Creates a successful conversion result. </summary>
    /// <param name="hasFailedTests"> Indicates whether converted results contain failing tests. </param>
    /// <param name="reportedTestCaseCount"> The number of reported test cases. </param>
    /// <returns> The successful conversion result. </returns>
    public static UnityResultsConversionResult Success (
        bool hasFailedTests,
        int reportedTestCaseCount = 1)
    {
        if (reportedTestCaseCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reportedTestCaseCount), reportedTestCaseCount, "Reported test case count must not be negative.");
        }

        return new UnityResultsConversionResult(hasFailedTests, reportedTestCaseCount, null, null);
    }

    /// <summary> Creates a failed conversion result. </summary>
    /// <param name="failureKind"> The failure kind. </param>
    /// <param name="errorMessage"> The user-facing failure message. </param>
    /// <returns> The failed conversion result. </returns>
    public static UnityResultsConversionResult Failure (
        UnityResultsConversionFailureKind failureKind,
        string errorMessage)
    {
        return new UnityResultsConversionResult(false, 0, failureKind, errorMessage);
    }
}
