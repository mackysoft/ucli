namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Converts path input values into full paths without leaking path format exceptions. </summary>
internal static class PathNormalizer
{
    /// <summary> Attempts to normalize one path value into a full path. </summary>
    /// <param name="pathValue"> The path value to normalize. </param>
    /// <param name="basePath"> The optional base path used to resolve relative <paramref name="pathValue" /> values. </param>
    /// <returns> The full path normalization result. </returns>
    public static FullPathNormalizationResult TryNormalizeFullPath (
        string? pathValue,
        string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return FullPathNormalizationResult.Failure(
                PathNormalizationFailureKind.EmptyPath,
                "Path value must not be empty.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return FullPathNormalizationResult.Success(Path.GetFullPath(pathValue));
            }

            var basePathResult = TryNormalizeFullPath(basePath);
            if (!basePathResult.IsSuccess)
            {
                return FullPathNormalizationResult.Failure(
                    basePathResult.FailureKind,
                    $"Base path is invalid. {basePathResult.DiagnosticMessage}");
            }

            return FullPathNormalizationResult.Success(Path.GetFullPath(Path.Combine(basePathResult.FullPath!, pathValue)));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return FullPathNormalizationResult.Failure(
                PathNormalizationFailureKind.InvalidFormat,
                exception.Message);
        }
    }
}
