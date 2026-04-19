namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Represents one Unity results conversion result. </summary>
/// <param name="HasFailedTests"> Indicates whether converted results contain failing tests when successful; otherwise <see langword="false" />. </param>
/// <param name="FailureKind"> The conversion failure kind on failure; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The user-facing conversion failure message on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityResultsConversionResult (
    bool HasFailedTests,
    UnityResultsConversionFailureKind? FailureKind,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether conversion succeeded. </summary>
    public bool IsSuccess => FailureKind is null;

    /// <summary> Creates a successful conversion result. </summary>
    /// <param name="hasFailedTests"> Indicates whether converted results contain failing tests. </param>
    /// <returns> The successful conversion result. </returns>
    public static UnityResultsConversionResult Success (bool hasFailedTests)
    {
        return new UnityResultsConversionResult(hasFailedTests, null, null);
    }

    /// <summary> Creates a failed conversion result. </summary>
    /// <param name="failureKind"> The failure kind. </param>
    /// <param name="errorMessage"> The user-facing failure message. </param>
    /// <returns> The failed conversion result. </returns>
    public static UnityResultsConversionResult Failure (
        UnityResultsConversionFailureKind failureKind,
        string errorMessage)
    {
        return new UnityResultsConversionResult(false, failureKind, errorMessage);
    }
}