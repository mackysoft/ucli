namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Represents the result of converting one path value into a normalized full path. </summary>
internal sealed class FullPathNormalizationResult
{
    private FullPathNormalizationResult (
        bool isSuccess,
        string? fullPath,
        PathNormalizationFailureKind failureKind,
        string diagnosticMessage)
    {
        IsSuccess = isSuccess;
        FullPath = fullPath;
        FailureKind = failureKind;
        DiagnosticMessage = diagnosticMessage;
    }

    /// <summary> Gets whether path normalization succeeded. </summary>
    public bool IsSuccess { get; }

    /// <summary> Gets the normalized full path on success; otherwise <see langword="null" />. </summary>
    public string? FullPath { get; }

    /// <summary> Gets the machine-readable failure kind. </summary>
    public PathNormalizationFailureKind FailureKind { get; }

    /// <summary> Gets the diagnostic failure message. </summary>
    public string DiagnosticMessage { get; }

    /// <summary> Creates a successful full path normalization result. </summary>
    /// <param name="fullPath"> The normalized full path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="fullPath" /> is empty. </exception>
    public static FullPathNormalizationResult Success (string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("Normalized full path must not be empty.", nameof(fullPath));
        }

        return new FullPathNormalizationResult(
            isSuccess: true,
            fullPath,
            PathNormalizationFailureKind.None,
            string.Empty);
    }

    /// <summary> Creates a failed full path normalization result. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="diagnosticMessage"> The diagnostic failure message. </param>
    /// <returns> The failed result. </returns>
    public static FullPathNormalizationResult Failure (
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

        return new FullPathNormalizationResult(
            isSuccess: false,
            fullPath: null,
            failureKind,
            diagnosticMessage);
    }
}
