namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Represents the result of normalizing one test-run path value. </summary>
/// <param name="IsSuccess"> Whether path normalization succeeded. </param>
/// <param name="FullPath"> The normalized full path when normalization succeeded; otherwise <see langword="null" />. </param>
/// <param name="FailureKind"> The machine-readable failure kind. </param>
/// <param name="DiagnosticMessage"> The diagnostic failure message. </param>
internal readonly record struct TestRunPathNormalizationResult (
    bool IsSuccess,
    string? FullPath,
    TestRunPathNormalizationFailureKind FailureKind,
    string DiagnosticMessage)
{
    /// <summary> Creates a successful path normalization result. </summary>
    /// <param name="fullPath"> The normalized full path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="fullPath" /> is <see langword="null" />. </exception>
    public static TestRunPathNormalizationResult Success (string fullPath)
    {
        if (fullPath == null)
        {
            throw new ArgumentNullException(nameof(fullPath));
        }

        return new TestRunPathNormalizationResult(
            IsSuccess: true,
            FullPath: fullPath,
            FailureKind: TestRunPathNormalizationFailureKind.None,
            DiagnosticMessage: string.Empty);
    }

    /// <summary> Creates a failed path normalization result. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="diagnosticMessage"> The diagnostic failure message. </param>
    /// <returns> The failed result. </returns>
    public static TestRunPathNormalizationResult Failure (
        TestRunPathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        if (failureKind == TestRunPathNormalizationFailureKind.None)
        {
            throw new ArgumentException("Failure kind must not be None.", nameof(failureKind));
        }

        return new TestRunPathNormalizationResult(
            IsSuccess: false,
            FullPath: null,
            FailureKind: failureKind,
            DiagnosticMessage: diagnosticMessage ?? string.Empty);
    }
}
