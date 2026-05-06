namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Represents the result of converting one path value into a normalized full path. </summary>
/// <param name="IsSuccess"> Whether path normalization succeeded. </param>
/// <param name="FullPath"> The normalized full path when normalization succeeded; otherwise <see langword="null" />. </param>
/// <param name="FailureKind"> The machine-readable failure kind. </param>
/// <param name="DiagnosticMessage"> The diagnostic failure message. </param>
internal readonly record struct FullPathNormalizationResult (
    bool IsSuccess,
    string? FullPath,
    PathNormalizationFailureKind FailureKind,
    string DiagnosticMessage)
{
    /// <summary> Creates a successful full path normalization result. </summary>
    /// <param name="fullPath"> The normalized full path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="fullPath" /> is <see langword="null" />. </exception>
    public static FullPathNormalizationResult Success (string fullPath)
    {
        if (fullPath == null)
        {
            throw new ArgumentNullException(nameof(fullPath));
        }

        return new FullPathNormalizationResult(
            IsSuccess: true,
            FullPath: fullPath,
            FailureKind: PathNormalizationFailureKind.None,
            DiagnosticMessage: string.Empty);
    }

    /// <summary> Creates a failed full path normalization result. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="diagnosticMessage"> The diagnostic failure message. </param>
    /// <returns> The failed result. </returns>
    public static FullPathNormalizationResult Failure (
        PathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        if (failureKind == PathNormalizationFailureKind.None)
        {
            throw new ArgumentException("Failure kind must not be None.", nameof(failureKind));
        }

        return new FullPathNormalizationResult(
            IsSuccess: false,
            FullPath: null,
            FailureKind: failureKind,
            DiagnosticMessage: diagnosticMessage ?? string.Empty);
    }
}
