namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Represents the result of normalizing one path under a repository root boundary. </summary>
internal sealed class RepositoryPathNormalizationResult
{
    private RepositoryPathNormalizationResult (
        bool isSuccess,
        string? fullPath,
        string? repositoryRelativeSlashPath,
        PathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        IsSuccess = isSuccess;
        FullPath = fullPath;
        RepositoryRelativeSlashPath = repositoryRelativeSlashPath;
        FailureKind = failureKind;
        DiagnosticMessage = diagnosticMessage;
    }

    /// <summary> Gets whether path normalization succeeded. </summary>
    public bool IsSuccess { get; }

    /// <summary> Gets the normalized full path on success; otherwise <see langword="null" />. </summary>
    public string? FullPath { get; }

    /// <summary> Gets the slash-separated repository-relative path on success; otherwise <see langword="null" />. </summary>
    public string? RepositoryRelativeSlashPath { get; }

    /// <summary> Gets the machine-readable failure kind. </summary>
    public PathNormalizationFailureKind FailureKind { get; }

    /// <summary> Gets the diagnostic failure message. </summary>
    public string DiagnosticMessage { get; }

    /// <summary> Creates a successful repository path normalization result. </summary>
    /// <param name="fullPath"> The normalized full path. </param>
    /// <param name="repositoryRelativeSlashPath"> The repository-relative slash-separated path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="fullPath" /> or <paramref name="repositoryRelativeSlashPath" /> is empty. </exception>
    public static RepositoryPathNormalizationResult Success (
        string fullPath,
        string repositoryRelativeSlashPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("Normalized full path must not be empty.", nameof(fullPath));
        }

        if (string.IsNullOrWhiteSpace(repositoryRelativeSlashPath))
        {
            throw new ArgumentException("Repository-relative path must not be empty.", nameof(repositoryRelativeSlashPath));
        }

        return new RepositoryPathNormalizationResult(
            isSuccess: true,
            fullPath,
            repositoryRelativeSlashPath,
            PathNormalizationFailureKind.None,
            string.Empty);
    }

    /// <summary> Creates a failed repository path normalization result. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="diagnosticMessage"> The diagnostic failure message. </param>
    /// <returns> The failed result. </returns>
    public static RepositoryPathNormalizationResult Failure (
        PathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        if (failureKind == PathNormalizationFailureKind.None
            || !Enum.IsDefined(typeof(PathNormalizationFailureKind), failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Failure kind must be defined and non-None.");
        }

        if (diagnosticMessage == null)
        {
            throw new ArgumentNullException(nameof(diagnosticMessage));
        }

        return new RepositoryPathNormalizationResult(
            isSuccess: false,
            fullPath: null,
            repositoryRelativeSlashPath: null,
            failureKind,
            diagnosticMessage);
    }
}
