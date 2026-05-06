namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Represents the result of normalizing one path under a repository root boundary. </summary>
/// <param name="IsSuccess"> Whether path normalization succeeded. </param>
/// <param name="FullPath"> The normalized full path when normalization succeeded; otherwise <see langword="null" />. </param>
/// <param name="RepositoryRelativeSlashPath"> The repository-relative slash-separated path when normalization succeeded; otherwise <see langword="null" />. </param>
/// <param name="FailureKind"> The machine-readable failure kind. </param>
/// <param name="DiagnosticMessage"> The diagnostic failure message. </param>
internal readonly record struct RepositoryPathNormalizationResult (
    bool IsSuccess,
    string? FullPath,
    string? RepositoryRelativeSlashPath,
    PathNormalizationFailureKind FailureKind,
    string DiagnosticMessage)
{
    /// <summary> Creates a successful repository path normalization result. </summary>
    /// <param name="fullPath"> The normalized full path. </param>
    /// <param name="repositoryRelativeSlashPath"> The repository-relative slash-separated path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="fullPath" /> or <paramref name="repositoryRelativeSlashPath" /> is <see langword="null" />. </exception>
    public static RepositoryPathNormalizationResult Success (
        string fullPath,
        string repositoryRelativeSlashPath)
    {
        if (fullPath == null)
        {
            throw new ArgumentNullException(nameof(fullPath));
        }

        if (repositoryRelativeSlashPath == null)
        {
            throw new ArgumentNullException(nameof(repositoryRelativeSlashPath));
        }

        return new RepositoryPathNormalizationResult(
            IsSuccess: true,
            FullPath: fullPath,
            RepositoryRelativeSlashPath: repositoryRelativeSlashPath,
            FailureKind: PathNormalizationFailureKind.None,
            DiagnosticMessage: string.Empty);
    }

    /// <summary> Creates a failed repository path normalization result. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="diagnosticMessage"> The diagnostic failure message. </param>
    /// <returns> The failed result. </returns>
    public static RepositoryPathNormalizationResult Failure (
        PathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        if (failureKind == PathNormalizationFailureKind.None)
        {
            throw new ArgumentException("Failure kind must not be None.", nameof(failureKind));
        }

        return new RepositoryPathNormalizationResult(
            IsSuccess: false,
            FullPath: null,
            RepositoryRelativeSlashPath: null,
            FailureKind: failureKind,
            DiagnosticMessage: diagnosticMessage ?? string.Empty);
    }
}
