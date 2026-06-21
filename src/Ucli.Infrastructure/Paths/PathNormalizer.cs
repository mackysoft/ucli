namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Converts path input values into full paths without leaking path format exceptions. </summary>
internal static class PathNormalizer
{
    /// <summary> Determines whether <paramref name="pathValue" /> is fully qualified on the current platform. </summary>
    /// <param name="pathValue"> The path value to inspect. </param>
    /// <returns> <see langword="true" /> when the path is fully qualified; otherwise <see langword="false" />. </returns>
    public static bool IsFullyQualifiedPath (string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(pathValue) || !Path.IsPathRooted(pathValue))
        {
            return false;
        }

        if (Path.DirectorySeparatorChar == '\\')
        {
            return IsWindowsDriveAbsolutePath(pathValue) || IsWindowsUncAbsolutePath(pathValue);
        }

        return pathValue[0] == Path.DirectorySeparatorChar;
    }

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

    private static bool IsWindowsDriveAbsolutePath (string pathValue)
    {
        return pathValue.Length >= 3
            && char.IsLetter(pathValue[0])
            && pathValue[1] == ':'
            && IsDirectorySeparator(pathValue[2]);
    }

    private static bool IsWindowsUncAbsolutePath (string pathValue)
    {
        return pathValue.Length >= 5
            && IsDirectorySeparator(pathValue[0])
            && IsDirectorySeparator(pathValue[1])
            && !IsDirectorySeparator(pathValue[2]);
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value == '\\' || value == '/';
    }
}
